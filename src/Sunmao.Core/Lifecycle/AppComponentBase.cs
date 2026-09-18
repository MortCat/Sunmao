using Sunmao.Core.Logging;

namespace Sunmao.Core.Lifecycle;

/// <summary>
/// Lifecycle base for Components. It centralizes state transitions and serializes lifecycle calls;
/// concrete Components expose their own typed operations and snapshots.
/// </summary>
/// <remarks>
/// <para>
/// Derived classes override the <c>On…</c> hooks instead of the lifecycle methods. Periodic work
/// belongs to private <c>PollingTaskBase</c> members that the Component starts and stops from its
/// hooks; a Component never inherits a poller.
/// </para>
/// <para>
/// Business operations that must not interleave with Initialize/Start/Stop/Dispose run through
/// <see cref="RunExclusiveAsync"/>, which shares the lifecycle gate. Lifecycle hooks,
/// <see cref="OnStateChanged"/> and <see cref="StateChanged"/> handlers run while that gate is
/// held, so they must not call lifecycle methods or <see cref="RunExclusiveAsync"/>.
/// </para>
/// <para>
/// Cancellation tokens passed to lifecycle methods cancel waiting for the gate and are forwarded
/// to the hook. A hook that must always run to completion (for example disarming hardware on
/// stop) should ignore the token for that part.
/// </para>
/// </remarks>
public abstract class AppComponentBase : IAppComponent
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly ComponentStateTracker _stateTracker = new();
    private readonly TaskCompletionSource<bool> _disposeCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ILogSink _log;

    private int _disposeStarted;
    private volatile bool _disposed;

    /// <summary>Creates the Component in the <see cref="ComponentState.Created"/> state.</summary>
    /// <param name="isEnabled">False to make the Component permanently <see cref="ComponentState.Disabled"/>.</param>
    /// <param name="logSink">Receives hook and observer failures; defaults to <see cref="NullLogSink"/>.</param>
    protected AppComponentBase(bool isEnabled, ILogSink? logSink = null)
    {
        IsEnabled = isEnabled;
        _log = logSink ?? NullLogSink.Instance;
        _stateTracker.Changed += ForwardStateChanged;
    }

    /// <inheritdoc />
    public bool IsEnabled { get; }

    /// <inheritdoc />
    public ComponentState State => _stateTracker.State;

    /// <inheritdoc />
    /// <remarks>
    /// Handlers run synchronously while the lifecycle gate is held. A throwing handler is logged and
    /// does not affect other handlers or the transition.
    /// </remarks>
    public event EventHandler<ComponentStateChangedEventArgs>? StateChanged;

    /// <summary>The log sink supplied to the constructor.</summary>
    protected ILogSink Log => _log;

    /// <summary>True once <see cref="DisposeAsync"/> has been called, including while it runs.</summary>
    protected bool IsDisposeStarted => Volatile.Read(ref _disposeStarted) != 0;

    /// <summary>True once disposal has stopped the Component and is releasing its resources.</summary>
    protected bool IsDisposed => _disposed;

    /// <inheritdoc />
    /// <remarks>
    /// Idempotent once the Component is Ready, Running or Stopped. A failure leaves the Component
    /// <see cref="ComponentState.Faulted"/> and rethrows.
    /// </remarks>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();

            if (!IsEnabled)
            {
                SetState(ComponentState.Disabled);
                return;
            }

            switch (State)
            {
                case ComponentState.Ready:
                case ComponentState.Starting:
                case ComponentState.Running:
                case ComponentState.Stopped:
                case ComponentState.Disabled:
                    return;
                case ComponentState.Faulted:
                    throw new InvalidOperationException("The Component is faulted and cannot be initialized again.");
                case ComponentState.Initializing:
                    throw new InvalidOperationException("The Component is already initializing.");
                case ComponentState.Created:
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported Component state: {State}.");
            }

            SetState(ComponentState.Initializing);
            try
            {
                await OnInitializeAsync(cancellationToken).ConfigureAwait(false);
                SetState(ComponentState.Ready);
            }
            catch (Exception exception)
            {
                SetState(ComponentState.Faulted, exception);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Requires a completed initialization; idempotent while Starting or Running. On failure,
    /// <see cref="OnStartFailedAsync"/> runs first, then the Component becomes
    /// <see cref="ComponentState.Faulted"/> and the start exception is rethrown.
    /// </remarks>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();

            if (!IsEnabled || State == ComponentState.Disabled)
            {
                return;
            }

            if (State is ComponentState.Starting or ComponentState.Running)
            {
                return;
            }

            if (State is ComponentState.Created or ComponentState.Initializing)
            {
                throw new InvalidOperationException("InitializeAsync must complete before StartAsync.");
            }

            if (State == ComponentState.Faulted)
            {
                throw new InvalidOperationException("The Component is faulted and cannot be started.");
            }

            if (State is not (ComponentState.Ready or ComponentState.Stopped))
            {
                throw new InvalidOperationException($"The Component cannot start from state {State}.");
            }

            SetState(ComponentState.Starting);
            try
            {
                await OnStartAsync(cancellationToken).ConfigureAwait(false);
                SetState(ComponentState.Running);
            }
            catch (Exception exception)
            {
                await RunStartFailureCleanupAsync(exception).ConfigureAwait(false);
                SetState(ComponentState.Faulted, exception);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// No-op when the Component never started or is already stopped. Once disposal has begun, it
    /// waits for disposal to finish instead. A failing <see cref="OnStopAsync"/> leaves the Component
    /// <see cref="ComponentState.Faulted"/> and rethrows.
    /// </remarks>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposeStarted) != 0)
        {
            await _disposeCompletion.Task.ConfigureAwait(false);
            return;
        }

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            await StopCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Stops the Component if needed, then runs <see cref="OnDisposeAsync"/>. Concurrent callers share
    /// one disposal and observe the same outcome.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeStarted, 1, 0) != 0)
        {
            await _disposeCompletion.Task.ConfigureAwait(false);
            GC.SuppressFinalize(this);
            return;
        }

        Exception? failure = null;
        try
        {
            await _lifecycleGate.WaitAsync().ConfigureAwait(false);
            try
            {
                try
                {
                    await StopCoreAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }

                _disposed = true;

                try
                {
                    await OnDisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    failure = Combine(failure, exception);
                }
            }
            finally
            {
                _lifecycleGate.Release();
            }

            if (failure is null)
            {
                _disposeCompletion.TrySetResult(true);
            }
            else
            {
                _disposeCompletion.TrySetException(failure);
                throw failure;
            }
        }
        catch (Exception exception)
        {
            _disposeCompletion.TrySetException(exception);
            throw;
        }
        finally
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>Called once during initialization. It must not start long-running work.</summary>
    /// <param name="cancellationToken">Token passed to <see cref="InitializeAsync"/>.</param>
    protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Called after initialization, when the Component should start its work.</summary>
    /// <param name="cancellationToken">Token passed to <see cref="StartAsync"/>.</param>
    protected virtual Task OnStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Called when the Component must stop accepting work and drain its workers.</summary>
    /// <param name="cancellationToken">Token passed to <see cref="StopAsync"/>; <c>None</c> during disposal.</param>
    protected virtual Task OnStopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Called once after the Component has stopped, to release its resources.</summary>
    protected virtual ValueTask OnDisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// Called when <see cref="OnStartAsync"/> fails, before the Component becomes Faulted, to release
    /// whatever the partial start acquired. Failures are logged and never replace the start error.
    /// </summary>
    /// <param name="startFailure">The exception thrown by <see cref="OnStartAsync"/>.</param>
    protected virtual Task OnStartFailedAsync(Exception startFailure) => Task.CompletedTask;

    /// <summary>
    /// Called for every state transition before external <see cref="StateChanged"/> observers.
    /// Runs while the lifecycle gate is held; failures are logged.
    /// </summary>
    /// <param name="args">The transition.</param>
    protected virtual void OnStateChanged(ComponentStateChangedEventArgs args)
    {
    }

    /// <summary>
    /// Runs a business operation under the lifecycle gate so it cannot interleave with lifecycle
    /// transitions. Availability is checked after the gate is acquired.
    /// </summary>
    /// <param name="operation">The operation. It must not call lifecycle methods.</param>
    /// <param name="cancellationToken">Cancels waiting for the gate and is passed to the operation.</param>
    /// <param name="allowWhileDisposing">
    /// True to let the operation run while disposal has been requested but has not yet stopped the
    /// Component; it becomes a no-op once the Component is disposed. Useful for safety operations
    /// such as disarming hardware.
    /// </param>
    /// <exception cref="ObjectDisposedException">The Component is disposed and <paramref name="allowWhileDisposing"/> is false.</exception>
    protected async Task RunExclusiveAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default,
        bool allowWhileDisposing = false)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (allowWhileDisposing)
            {
                if (_disposed)
                {
                    return;
                }
            }
            else
            {
                ThrowIfUnavailable();
            }

            await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>Runs a business operation that returns a value under the lifecycle gate.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="operation">The operation. It must not call lifecycle methods.</param>
    /// <param name="cancellationToken">Cancels waiting for the gate and is passed to the operation.</param>
    /// <exception cref="ObjectDisposedException">The Component is disposed or disposing.</exception>
    protected async Task<T> RunExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfUnavailable();
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Sets the state directly. Intended for Components that report extra states from their hooks;
    /// normal transitions are handled by the lifecycle methods.
    /// </summary>
    /// <param name="state">New state.</param>
    /// <param name="error">Failure that caused the transition, if any.</param>
    protected void SetState(ComponentState state, Exception? error = null) =>
        _stateTracker.Set(state, error);

    private async Task RunStartFailureCleanupAsync(Exception startFailure)
    {
        try
        {
            await OnStartFailedAsync(startFailure).ConfigureAwait(false);
        }
        catch (Exception cleanupException)
        {
            _log.Write(LogLevel.Error, GetType().Name, "Component startup cleanup failed.", cleanupException);
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        if (State is ComponentState.Created or ComponentState.Disabled or ComponentState.Stopped)
        {
            return;
        }

        SetState(ComponentState.Stopping);
        try
        {
            await OnStopAsync(cancellationToken).ConfigureAwait(false);
            SetState(ComponentState.Stopped);
        }
        catch (Exception exception)
        {
            SetState(ComponentState.Faulted, exception);
            throw;
        }
    }

    private void ForwardStateChanged(object? sender, ComponentStateChangedEventArgs args)
    {
        try
        {
            OnStateChanged(args);
        }
        catch (Exception exception)
        {
            _log.Write(LogLevel.Warning, GetType().Name, "A Component state hook failed.", exception);
        }

        var handler = StateChanged;
        if (handler is null)
        {
            return;
        }

        foreach (var subscriber in handler.GetInvocationList())
        {
            try
            {
                ((EventHandler<ComponentStateChangedEventArgs>)subscriber)(this, args);
            }
            catch (Exception exception)
            {
                _log.Write(LogLevel.Warning, GetType().Name, "A Component state observer failed.", exception);
            }
        }
    }

    private void ThrowIfUnavailable()
    {
        if (_disposed || Volatile.Read(ref _disposeStarted) != 0)
        {
            throw new ObjectDisposedException(GetType().Name);
        }
    }

    private static Exception Combine(Exception? first, Exception second) => first is null
        ? second
        : new AggregateException(first, second);
}
