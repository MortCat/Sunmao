using System.Diagnostics;
using Sunmao.Core.Logging;

namespace Sunmao.Wpf.Polling;

/// <summary>
/// Deterministic core behind <see cref="UiPollingTimer"/> and <see cref="ManualUiPulseDriver"/>.
/// </summary>
/// <remarks>
/// It owns only registrations and pulse sequencing, and never performs I/O, awaits, starts a task or
/// touches a view model from another thread. A callback failure is isolated to that registration, and
/// a nested pulse is ignored because the UI only needs the newest snapshot.
/// </remarks>
internal sealed class UiPollingCoordinator : IUiPollingSource, IUiPollingLifetime
{
    private readonly List<Registration> _registrations = [];
    private readonly ILogSink _log;
    private readonly TimeSpan _slowCallbackThreshold;
    private readonly Dictionary<string, DateTimeOffset> _lastDiagnosticAt = new(StringComparer.Ordinal);
    private readonly int _ownerThreadId;
    private bool _isRunning;
    private bool _isDisposed;
    private bool _isPulsing;
    private long _sequence;

    public UiPollingCoordinator(ILogSink? logSink = null, TimeSpan? slowCallbackThreshold = null)
    {
        _ownerThreadId = UiThreadGuard.Capture();
        _log = logSink ?? NullLogSink.Instance;
        _slowCallbackThreshold = slowCallbackThreshold ?? TimeSpan.FromMilliseconds(50);
        if (_slowCallbackThreshold < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(slowCallbackThreshold));
        }
    }

    public int Hertz => 0;

    public bool IsRunning
    {
        get
        {
            VerifyUi(nameof(IsRunning));
            return _isRunning;
        }
    }

    internal bool IsDisposed => _isDisposed;

    public IUiPollingSubscription Register(string owner, Action<UiPulse> callback)
    {
        VerifyUi(nameof(Register));
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(callback);
        var registration = new Registration(owner.Trim(), callback);
        _registrations.Add(registration);
        return new Subscription(this, registration);
    }

    public void Start()
    {
        VerifyUi(nameof(Start));
        ThrowIfDisposed();
        _isRunning = true;
    }

    public void Stop()
    {
        VerifyUi(nameof(Stop));
        if (!_isDisposed)
        {
            _isRunning = false;
        }
    }

    /// <summary>Delivers one pulse to every enabled registration.</summary>
    public void Pulse(DateTimeOffset timestamp)
    {
        VerifyUi(nameof(Pulse));
        if (!_isRunning || _isDisposed || _isPulsing)
        {
            return;
        }

        _isPulsing = true;
        try
        {
            var pulse = new UiPulse(++_sequence, timestamp);
            foreach (var registration in _registrations.ToArray())
            {
                if (registration.IsDisposed || !registration.IsEnabled)
                {
                    continue;
                }

                var startedAt = Stopwatch.GetTimestamp();
                try
                {
                    registration.Callback(pulse);
                }
                catch (Exception exception)
                {
                    WriteThrottled(
                        $"error:{registration.Owner}",
                        LogLevel.Error,
                        registration.Owner,
                        "UI polling callback failed.",
                        exception);
                }

                var elapsed = Stopwatch.GetElapsedTime(startedAt);
                if (elapsed >= _slowCallbackThreshold)
                {
                    WriteThrottled(
                        $"slow:{registration.Owner}",
                        LogLevel.Warning,
                        registration.Owner,
                        $"UI polling callback took {elapsed.TotalMilliseconds:0.##} ms.",
                        exception: null);
                }
            }
        }
        finally
        {
            _isPulsing = false;
        }
    }

    public void Dispose()
    {
        VerifyUi(nameof(Dispose));
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isRunning = false;
        foreach (var registration in _registrations)
        {
            registration.IsEnabled = false;
            registration.IsDisposed = true;
        }

        _registrations.Clear();
        _lastDiagnosticAt.Clear();
    }

    private void SetEnabled(Registration? registration, bool enabled)
    {
        if (_isDisposed)
        {
            return;
        }

        VerifyUi(enabled ? nameof(IUiPollingSubscription.Enable) : nameof(IUiPollingSubscription.Disable));
        if (registration is null || registration.IsDisposed)
        {
            return;
        }

        registration.IsEnabled = enabled;
    }

    private void Remove(Registration registration)
    {
        if (_isDisposed)
        {
            return;
        }

        VerifyUi(nameof(IUiPollingSubscription.Dispose));
        registration.IsEnabled = false;
        registration.IsDisposed = true;
        _registrations.Remove(registration);
    }

    private void WriteThrottled(string key, LogLevel level, string owner, string message, Exception? exception)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastDiagnosticAt.TryGetValue(key, out var last) && now - last < TimeSpan.FromSeconds(1))
        {
            return;
        }

        _lastDiagnosticAt[key] = now;
        try
        {
            _log.Write(level, $"ui-polling:{owner}", message, exception);
        }
        catch
        {
            // Diagnostics must never stop later subscribers from receiving the pulse.
        }
    }

    private void VerifyUi(string operation) => UiThreadGuard.Verify(_ownerThreadId, operation);

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(UiPollingCoordinator));
        }
    }

    private sealed class Registration(string owner, Action<UiPulse> callback)
    {
        public string Owner { get; } = owner;

        public Action<UiPulse> Callback { get; } = callback;

        public bool IsEnabled { get; set; }

        public bool IsDisposed { get; set; }
    }

    private sealed class Subscription(UiPollingCoordinator coordinator, Registration registration) : IUiPollingSubscription
    {
        private UiPollingCoordinator? _coordinator = coordinator;
        private Registration? _registration = registration;

        public bool IsEnabled
        {
            get
            {
                var owner = _coordinator ?? throw new ObjectDisposedException(nameof(Subscription));
                if (owner.IsDisposed)
                {
                    return false;
                }

                owner.VerifyUi(nameof(IsEnabled));
                return _registration is { IsEnabled: true, IsDisposed: false };
            }
        }

        public void Enable() =>
            (_coordinator ?? throw new ObjectDisposedException(nameof(Subscription))).SetEnabled(_registration, true);

        public void Disable() =>
            (_coordinator ?? throw new ObjectDisposedException(nameof(Subscription))).SetEnabled(_registration, false);

        public void Dispose()
        {
            var owner = Volatile.Read(ref _coordinator);
            var current = Volatile.Read(ref _registration);
            if (owner is not null && current is not null)
            {
                // Remove verifies thread affinity before this handle is cleared, so a caller on the
                // wrong thread can retry on the UI thread instead of leaking an enabled registration.
                owner.Remove(current);
                Interlocked.CompareExchange(ref _registration, null, current);
                Interlocked.CompareExchange(ref _coordinator, null, owner);
            }
        }
    }
}

/// <summary>Thread-affinity guard for UI-owned infrastructure, based on the creating thread.</summary>
internal static class UiThreadGuard
{
    public static int Capture() => Environment.CurrentManagedThreadId;

    public static void Verify(int ownerThreadId, string operation)
    {
        if (Environment.CurrentManagedThreadId != ownerThreadId)
        {
            throw new InvalidOperationException(
                $"{operation} must run on the UI thread that created the polling source.");
        }
    }
}
