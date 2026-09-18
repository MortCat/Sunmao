using System.Windows.Threading;
using Sunmao.Core.Logging;

namespace Sunmao.Wpf.Polling;

/// <summary>
/// The one UI-thread timer for the whole application; view models project backend snapshots on its
/// pulse instead of subscribing to backend events.
/// </summary>
/// <remarks>
/// <para>
/// Create it on the UI thread in the composition root, pass it to view models as
/// <see cref="IUiPollingSource"/>, start it once the shell exists and dispose it on shutdown. Ten
/// hertz is more than an operator can perceive and keeps the cost negligible.
/// </para>
/// <para>
/// Only read-only display state may be copied from a snapshot. Interactive state (text being typed,
/// the selected row, scroll position, filters) lives in the view model and must not be overwritten on
/// every pulse, or the operator loses what they were doing.
/// </para>
/// </remarks>
public sealed class UiPollingTimer : IUiPollingSource, IUiPollingLifetime
{
    private readonly DispatcherTimer _timer;
    private readonly UiPollingCoordinator _coordinator;
    private bool _disposed;
    private int _startCount;
    private int _stopCount;

    /// <summary>Creates a stopped timer on the current (UI) thread.</summary>
    /// <param name="hertz">Pulses per second, 1 to 60.</param>
    /// <param name="logSink">Receives callback failures and slow-callback warnings.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="hertz"/> is out of range.</exception>
    public UiPollingTimer(int hertz = 10, ILogSink? logSink = null)
    {
        if (hertz is < 1 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(hertz), hertz, "Poll rate must be 1-60 Hz.");
        }

        Hertz = hertz;
        _coordinator = new UiPollingCoordinator(logSink);
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1.0 / hertz)
        };
        _timer.Tick += OnTick;
    }

    /// <inheritdoc />
    public int Hertz { get; }

    /// <inheritdoc />
    public bool IsRunning => _timer.IsEnabled;

    /// <summary>How many times the timer actually started; a composition root starts it once.</summary>
    public int StartCount => Volatile.Read(ref _startCount);

    /// <summary>How many times a running timer actually stopped.</summary>
    public int StopCount => Volatile.Read(ref _stopCount);

    /// <inheritdoc />
    public IUiPollingSubscription Register(string owner, Action<UiPulse> callback) =>
        _coordinator.Register(owner, callback);

    /// <inheritdoc />
    public void Start()
    {
        ThrowIfDisposed();
        if (_timer.IsEnabled)
        {
            return;
        }

        _coordinator.Start();
        _timer.Start();
        Interlocked.Increment(ref _startCount);
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        if (_timer.IsEnabled)
        {
            _timer.Stop();
            Interlocked.Increment(ref _stopCount);
        }

        _coordinator.Stop();
    }

    /// <summary>Stops the timer and removes every registration. Call on the UI thread.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_timer.IsEnabled)
        {
            _timer.Stop();
            Interlocked.Increment(ref _stopCount);
        }

        _timer.Tick -= OnTick;
        _coordinator.Dispose();
    }

    private void OnTick(object? sender, EventArgs args) => _coordinator.Pulse(DateTimeOffset.UtcNow);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(UiPollingTimer));
        }
    }
}

/// <summary>
/// UI pulse source driven by explicit <see cref="Pulse"/> calls instead of a timer, for tests and
/// headless hosts. It has the same registration and thread-affinity rules as <see cref="UiPollingTimer"/>.
/// </summary>
public sealed class ManualUiPulseDriver : IUiPollingSource, IUiPollingLifetime
{
    private readonly UiPollingCoordinator _coordinator;

    /// <summary>Creates a stopped driver bound to the current thread.</summary>
    /// <param name="logSink">Receives callback failures and slow-callback warnings.</param>
    public ManualUiPulseDriver(ILogSink? logSink = null)
    {
        _coordinator = new UiPollingCoordinator(logSink);
    }

    /// <inheritdoc />
    public int Hertz => 0;

    /// <inheritdoc />
    public bool IsRunning => _coordinator.IsRunning;

    /// <inheritdoc />
    public IUiPollingSubscription Register(string owner, Action<UiPulse> callback) =>
        _coordinator.Register(owner, callback);

    /// <inheritdoc />
    public void Start() => _coordinator.Start();

    /// <inheritdoc />
    public void Stop() => _coordinator.Stop();

    /// <summary>Delivers one pulse to every enabled registration, if started.</summary>
    /// <param name="timestamp">Pulse time; defaults to now.</param>
    public void Pulse(DateTimeOffset? timestamp = null) =>
        _coordinator.Pulse(timestamp ?? DateTimeOffset.UtcNow);

    /// <summary>Removes every registration.</summary>
    public void Dispose() => _coordinator.Dispose();
}
