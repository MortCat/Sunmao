namespace Sunmao.Wpf.Polling;

/// <summary>
/// Registration port for the application's shared UI pulse.
/// </summary>
/// <remarks>
/// View models receive this port, never the lifetime controller: a page can enable or disable its own
/// subscription but cannot stop the timer the rest of the application uses. All members are
/// UI-thread-only.
/// </remarks>
public interface IUiPollingSource
{
    /// <summary>Registers a callback, initially disabled.</summary>
    /// <param name="owner">Name used in diagnostics, usually the page name.</param>
    /// <param name="callback">Short, synchronous projection of backend snapshots; no I/O or waiting.</param>
    /// <returns>The subscription handle; dispose it when the page is disposed.</returns>
    IUiPollingSubscription Register(string owner, Action<UiPulse> callback);
}

/// <summary>One owner's enable/disable handle on the shared UI pulse.</summary>
public interface IUiPollingSubscription : IDisposable
{
    /// <summary>Whether the callback currently receives pulses.</summary>
    bool IsEnabled { get; }

    /// <summary>Starts delivering pulses, typically when the page is activated.</summary>
    void Enable();

    /// <summary>Stops delivering pulses, typically when the page is deactivated.</summary>
    void Disable();
}

/// <summary>Lifetime control of the physical UI pulse. Owned by the composition root only.</summary>
public interface IUiPollingLifetime : IDisposable
{
    /// <summary>Pulse rate, or 0 for a manually driven source.</summary>
    int Hertz { get; }

    /// <summary>Whether pulses are being delivered.</summary>
    bool IsRunning { get; }

    /// <summary>Starts delivering pulses.</summary>
    void Start();

    /// <summary>Stops delivering pulses without removing registrations.</summary>
    void Stop();
}

/// <summary>Value delivered to every enabled subscriber on one pulse.</summary>
/// <param name="Sequence">Monotonic pulse number, starting at 1.</param>
/// <param name="Timestamp">Time of the pulse.</param>
public readonly record struct UiPulse(long Sequence, DateTimeOffset Timestamp);

/// <summary>
/// A source that never pulses, for headless callers that do not need periodic projection. It creates
/// no timer or worker.
/// </summary>
public sealed class InactiveUiPollingSource : IUiPollingSource
{
    private InactiveUiPollingSource()
    {
    }

    /// <summary>Shared instance.</summary>
    public static InactiveUiPollingSource Instance { get; } = new();

    /// <inheritdoc />
    public IUiPollingSubscription Register(string owner, Action<UiPulse> callback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(callback);
        return new Subscription();
    }

    private sealed class Subscription : IUiPollingSubscription
    {
        private int _enabled;
        private int _disposed;

        public bool IsEnabled => Volatile.Read(ref _enabled) != 0 && Volatile.Read(ref _disposed) == 0;

        public void Enable()
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                Volatile.Write(ref _enabled, 1);
            }
        }

        public void Disable() => Volatile.Write(ref _enabled, 0);

        public void Dispose()
        {
            Volatile.Write(ref _enabled, 0);
            Interlocked.Exchange(ref _disposed, 1);
        }
    }
}
