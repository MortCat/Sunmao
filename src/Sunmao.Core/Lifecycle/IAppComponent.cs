namespace Sunmao.Core.Lifecycle;

/// <summary>Lifecycle state of a Component.</summary>
public enum ComponentState
{
    /// <summary>Constructed; no resources have been prepared.</summary>
    Created,

    /// <summary><see cref="IAppComponent.InitializeAsync"/> is running.</summary>
    Initializing,

    /// <summary>Initialized and ready to start.</summary>
    Ready,

    /// <summary><see cref="IAppComponent.StartAsync"/> is running.</summary>
    Starting,

    /// <summary>Started and doing its work.</summary>
    Running,

    /// <summary><see cref="IAppComponent.StopAsync"/> is running.</summary>
    Stopping,

    /// <summary>Stopped; it can be started again.</summary>
    Stopped,

    /// <summary>A lifecycle step failed; the Component cannot be initialized or started again.</summary>
    Faulted,

    /// <summary>The Component is disabled by configuration and never starts.</summary>
    Disabled
}

/// <summary>Describes one observable Component state transition.</summary>
public sealed class ComponentStateChangedEventArgs : EventArgs
{
    /// <summary>Creates the event data for a transition.</summary>
    /// <param name="previous">State before the transition.</param>
    /// <param name="current">State after the transition.</param>
    /// <param name="error">Failure that caused the transition, if any.</param>
    public ComponentStateChangedEventArgs(
        ComponentState previous,
        ComponentState current,
        Exception? error = null)
    {
        Previous = previous;
        Current = current;
        Error = error;
        ChangedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>State before the transition.</summary>
    public ComponentState Previous { get; }

    /// <summary>State after the transition.</summary>
    public ComponentState Current { get; }

    /// <summary>Failure that caused the transition, for example into <see cref="ComponentState.Faulted"/>.</summary>
    public Exception? Error { get; }

    /// <summary>Wall-clock time of the transition.</summary>
    public DateTimeOffset ChangedAt { get; }
}

/// <summary>
/// Common lifecycle contract for long-lived Components.
/// </summary>
/// <remarks>
/// The lifecycle is <c>InitializeAsync</c> → <c>StartAsync</c> → <c>StopAsync</c> →
/// <c>DisposeAsync</c>. Business operations deliberately stay outside this interface, on each
/// Component's own typed interface. Implement it by deriving from <see cref="AppComponentBase"/>.
/// </remarks>
public interface IAppComponent : IAsyncDisposable
{
    /// <summary>False when configuration disables the Component; it then never starts.</summary>
    bool IsEnabled { get; }

    /// <summary>Current lifecycle state.</summary>
    ComponentState State { get; }

    /// <summary>Raised after every state transition.</summary>
    event EventHandler<ComponentStateChangedEventArgs>? StateChanged;

    /// <summary>Validates configuration and prepares resources. Must not start long-running work.</summary>
    /// <param name="cancellationToken">Cancels waiting for and running initialization.</param>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Starts the Component's work after initialization.</summary>
    /// <param name="cancellationToken">Cancels waiting for and running the start.</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stops accepting work, cancels workers and waits for them to drain.</summary>
    /// <param name="cancellationToken">Cancels waiting for and running the stop.</param>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Thread-safe state holder used by <see cref="AppComponentBase"/>. It holds no lifecycle rules.
/// </summary>
internal sealed class ComponentStateTracker
{
    private int _state = (int)ComponentState.Created;

    public ComponentState State => (ComponentState)Volatile.Read(ref _state);

    public event EventHandler<ComponentStateChangedEventArgs>? Changed;

    public void Set(ComponentState state, Exception? error = null)
    {
        var previous = (ComponentState)Interlocked.Exchange(ref _state, (int)state);
        if (previous == state)
        {
            return;
        }

        Changed?.Invoke(this, new ComponentStateChangedEventArgs(previous, state, error));
    }
}
