namespace Sunmao.Testing;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock only moves when a test calls <see cref="Advance"/>.
/// Timers created from it, including <c>Task.Delay(delay, provider)</c>, fire during
/// <see cref="Advance"/> in due-time order.
/// </summary>
/// <remarks>
/// <para>
/// Timer callbacks run synchronously on the thread that calls <see cref="Advance"/>; call it from one
/// thread at a time. A timer with a zero due time fires on the next <see cref="Advance"/>, including
/// <c>Advance(TimeSpan.Zero)</c>.
/// </para>
/// <para>
/// Code under test often creates its timer on another thread after an asynchronous start. Wait for
/// <see cref="ActiveTimerCount"/> before advancing, otherwise the timer may be created after the
/// advance and measured from the new time:
/// </para>
/// <code>
/// await TestWait.UntilAsync(() => time.ActiveTimerCount > 0);
/// time.Advance(TimeSpan.FromSeconds(1));
/// </code>
/// </remarks>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private readonly DateTimeOffset _start;
    private readonly TimeZoneInfo _localTimeZone;
    private long _elapsedTicks;

    /// <summary>Creates a clock whose wall-clock time starts at <paramref name="start"/>.</summary>
    /// <param name="start">Initial time; defaults to the current system time.</param>
    /// <param name="localTimeZone">
    /// Zone used by <see cref="TimeProvider.GetLocalNow"/>; defaults to UTC so date-dependent tests
    /// behave the same on every machine.
    /// </param>
    public ManualTimeProvider(DateTimeOffset? start = null, TimeZoneInfo? localTimeZone = null)
    {
        _start = (start ?? DateTimeOffset.UtcNow).ToUniversalTime();
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Utc;
    }

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => _localTimeZone;

    /// <summary>Time advanced since construction.</summary>
    public TimeSpan Elapsed => TimeSpan.FromTicks(Interlocked.Read(ref _elapsedTicks));

    /// <summary>Number of timers currently scheduled to fire.</summary>
    public int ActiveTimerCount
    {
        get
        {
            lock (_gate)
            {
                return _timers.Count;
            }
        }
    }

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override long GetTimestamp() => Interlocked.Read(ref _elapsedTicks);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _start.AddTicks(Interlocked.Read(ref _elapsedTicks));

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>
    /// Moves the clock forward, firing every timer that falls due on the way, in due-time order.
    /// The clock reads each timer's due time while its callback runs.
    /// </summary>
    /// <param name="duration">Non-negative amount of time.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is negative.</exception>
    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Time cannot move backwards.");
        }

        var target = Interlocked.Read(ref _elapsedTicks) + duration.Ticks;
        while (true)
        {
            ManualTimer? due;
            lock (_gate)
            {
                due = null;
                foreach (var timer in _timers)
                {
                    if (timer.DueTicks <= target && (due is null || timer.DueTicks < due.DueTicks))
                    {
                        due = timer;
                    }
                }

                if (due is null)
                {
                    break;
                }

                if (due.DueTicks > _elapsedTicks)
                {
                    Interlocked.Exchange(ref _elapsedTicks, due.DueTicks);
                }

                due.MarkFiredLocked();
            }

            due.InvokeCallback();
        }

        Interlocked.Exchange(ref _elapsedTicks, Math.Max(Interlocked.Read(ref _elapsedTicks), target));
    }

    private void Schedule(ManualTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
            timer.PeriodTicks = period == Timeout.InfiniteTimeSpan || period <= TimeSpan.Zero ? -1 : period.Ticks;
            if (dueTime == Timeout.InfiniteTimeSpan)
            {
                timer.DueTicks = long.MaxValue;
                return;
            }

            timer.DueTicks = _elapsedTicks + Math.Max(0, dueTime.Ticks);
            _timers.Add(timer);
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (_gate)
        {
            _timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private bool _disposed;

        public long DueTicks { get; set; } = long.MaxValue;

        public long PeriodTicks { get; set; } = -1;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
            {
                return false;
            }

            owner.Schedule(this, dueTime, period);
            return true;
        }

        /// <summary>Reschedules a periodic timer or unschedules a one-shot timer. Caller holds the gate.</summary>
        public void MarkFiredLocked()
        {
            if (PeriodTicks > 0)
            {
                DueTicks += PeriodTicks;
                return;
            }

            DueTicks = long.MaxValue;
            owner._timers.Remove(this);
        }

        public void InvokeCallback()
        {
            if (!_disposed)
            {
                callback(state);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            owner.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
