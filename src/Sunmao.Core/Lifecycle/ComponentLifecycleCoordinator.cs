namespace Sunmao.Core.Lifecycle;

/// <summary>
/// Runs the lifecycle of several Components in dependency order: initialize and start in
/// registration order, stop and dispose in reverse order.
/// </summary>
/// <remarks>
/// A composition root owns one coordinator. Register Components before calling
/// <see cref="InitializeAsync"/>; register a dependency before the Components that use it.
/// A failed initialize or start stops the Components already touched before rethrowing.
/// </remarks>
public sealed class ComponentLifecycleCoordinator : IAsyncDisposable
{
    private readonly List<IAppComponent> _components;
    private readonly object _componentsGate = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;
    private bool _started;
    private bool _disposed;

    /// <summary>Creates a coordinator with an optional initial set of Components.</summary>
    /// <param name="components">Components in dependency order.</param>
    /// <exception cref="ArgumentException">The collection contains null.</exception>
    public ComponentLifecycleCoordinator(IEnumerable<IAppComponent>? components = null)
    {
        _components = (components ?? []).ToList();
        if (_components.Any(component => component is null))
        {
            throw new ArgumentException("Component collection cannot contain null values.", nameof(components));
        }
    }

    /// <summary>Registered Components in startup order.</summary>
    public IReadOnlyList<IAppComponent> Components
    {
        get
        {
            lock (_componentsGate)
            {
                return _components.ToArray();
            }
        }
    }

    /// <summary>
    /// Registers a Component before lifecycle execution begins. Registering the same instance twice
    /// is ignored.
    /// </summary>
    /// <param name="component">The Component.</param>
    /// <param name="first">True to put it before every Component registered so far.</param>
    /// <exception cref="InvalidOperationException">Initialization has already run.</exception>
    public void Register(IAppComponent component, bool first = false)
    {
        ArgumentNullException.ThrowIfNull(component);
        lock (_componentsGate)
        {
            ThrowIfDisposed();
            if (_initialized || _started)
            {
                throw new InvalidOperationException(
                    "Components can only be registered before lifecycle execution begins.");
            }

            if (_components.Any(existing => ReferenceEquals(existing, component)))
            {
                return;
            }

            if (first)
            {
                _components.Insert(0, component);
            }
            else
            {
                _components.Add(component);
            }
        }
    }

    /// <summary>Initializes every Component in order. Idempotent after success.</summary>
    /// <param name="cancellationToken">Passed to each Component.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_initialized)
            {
                return;
            }

            try
            {
                foreach (var component in SnapshotComponents())
                {
                    await component.InitializeAsync(cancellationToken).ConfigureAwait(false);
                }

                _initialized = true;
            }
            catch
            {
                await StopAllAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Starts every Component in order. Idempotent while started.</summary>
    /// <param name="cancellationToken">Passed to each Component.</param>
    /// <exception cref="InvalidOperationException"><see cref="InitializeAsync"/> has not completed.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!_initialized)
            {
                throw new InvalidOperationException("Components must be initialized before they can start.");
            }

            if (_started)
            {
                return;
            }

            try
            {
                foreach (var component in SnapshotComponents())
                {
                    await component.StartAsync(cancellationToken).ConfigureAwait(false);
                }

                _started = true;
            }
            catch
            {
                await StopAllAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Stops every Component in reverse order. Every Component is asked to stop even when an earlier
    /// one fails; failures are rethrown together as an <see cref="AggregateException"/>.
    /// </summary>
    /// <param name="cancellationToken">Passed to each Component.</param>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            await StopAllAsync(cancellationToken, throwOnErrors: true).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Stops, then disposes every Component in reverse order. Failures are collected and rethrown as
    /// one <see cref="AggregateException"/> after every Component has been disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var errors = new List<Exception>();
            try
            {
                await StopAllAsync(CancellationToken.None, errors).ConfigureAwait(false);
            }
            finally
            {
                var components = SnapshotComponents();
                for (var index = components.Length - 1; index >= 0; index--)
                {
                    try
                    {
                        await components[index].DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        errors.Add(exception);
                    }
                }

                _started = false;
            }

            if (errors.Count > 0)
            {
                throw new AggregateException("One or more Components failed during shutdown.", errors);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopAllAsync(
        CancellationToken cancellationToken,
        List<Exception>? errors = null,
        bool throwOnErrors = false)
    {
        errors ??= [];
        var components = SnapshotComponents();
        for (var index = components.Length - 1; index >= 0; index--)
        {
            try
            {
                await components[index].StopAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        _started = false;

        if (throwOnErrors && errors.Count > 0)
        {
            throw new AggregateException("One or more Components failed to stop.", errors);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ComponentLifecycleCoordinator));
        }
    }

    private IAppComponent[] SnapshotComponents()
    {
        lock (_componentsGate)
        {
            return _components.ToArray();
        }
    }
}
