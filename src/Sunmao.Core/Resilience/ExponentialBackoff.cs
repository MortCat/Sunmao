namespace Sunmao.Core.Resilience;

/// <summary>
/// Tracks consecutive failures of one retried operation and decides when the next attempt is due.
/// </summary>
/// <remarks>
/// <para>
/// Typical use inside a poller: the first attempt after a resource drops runs immediately; each
/// failed attempt calls <see cref="RecordFailure"/>, which schedules the next attempt after
/// <c>initialDelay</c>, then doubles the delay up to <c>maximumDelay</c>; a success calls
/// <see cref="Reset"/>.
/// </para>
/// <code>
/// if (!_backoff.IsAttemptDue) return PollingResult.Continue;
/// if (await TryConnectAsync(ct)) _backoff.Reset(); else _backoff.RecordFailure();
/// </code>
/// <para>Not thread-safe: use it from a single owner loop.</para>
/// </remarks>
public sealed class ExponentialBackoff
{
    /// <summary>Default delay after the first failure: 0.5 seconds.</summary>
    public static readonly TimeSpan DefaultInitialDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>Default upper bound for the delay: 5 seconds.</summary>
    public static readonly TimeSpan DefaultMaximumDelay = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _timeProvider;
    private long _lastFailureTimestamp;

    /// <summary>Creates a tracker with no recorded failures.</summary>
    /// <param name="initialDelay">Delay after the first failure; defaults to <see cref="DefaultInitialDelay"/>.</param>
    /// <param name="maximumDelay">Upper bound for the delay; defaults to <see cref="DefaultMaximumDelay"/>.</param>
    /// <param name="multiplier">Growth factor per failure; defaults to 2.</param>
    /// <param name="timeProvider">Time source; defaults to the system.</param>
    /// <exception cref="ArgumentOutOfRangeException">A delay is not positive, the maximum is below the initial delay, or the multiplier is below 1.</exception>
    public ExponentialBackoff(
        TimeSpan? initialDelay = null,
        TimeSpan? maximumDelay = null,
        double multiplier = 2,
        TimeProvider? timeProvider = null)
    {
        InitialDelay = initialDelay ?? DefaultInitialDelay;
        MaximumDelay = maximumDelay ?? DefaultMaximumDelay;
        if (InitialDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(initialDelay), InitialDelay, "The initial delay must be positive.");
        }

        if (MaximumDelay < InitialDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDelay), MaximumDelay, "The maximum delay must not be below the initial delay.");
        }

        if (double.IsNaN(multiplier) || multiplier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "The multiplier must be at least 1.");
        }

        Multiplier = multiplier;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Delay after the first failure.</summary>
    public TimeSpan InitialDelay { get; }

    /// <summary>Upper bound for the delay.</summary>
    public TimeSpan MaximumDelay { get; }

    /// <summary>Growth factor per failure.</summary>
    public double Multiplier { get; }

    /// <summary>Consecutive failures since construction or the last <see cref="Reset"/>.</summary>
    public int Failures { get; private set; }

    /// <summary>Delay between the last failure and the next attempt; zero when no failure is recorded.</summary>
    public TimeSpan CurrentDelay { get; private set; }

    /// <summary>True when no failure is recorded or the current delay has elapsed since the last failure.</summary>
    public bool IsAttemptDue =>
        Failures == 0 || _timeProvider.GetElapsedTime(_lastFailureTimestamp) >= CurrentDelay;

    /// <summary>Time left until the next attempt is due; zero when it is already due.</summary>
    public TimeSpan TimeUntilNextAttempt
    {
        get
        {
            if (Failures == 0)
            {
                return TimeSpan.Zero;
            }

            var remaining = CurrentDelay - _timeProvider.GetElapsedTime(_lastFailureTimestamp);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>Records a failed attempt made now and grows the delay.</summary>
    public void RecordFailure()
    {
        Failures++;
        _lastFailureTimestamp = _timeProvider.GetTimestamp();
        if (Failures == 1)
        {
            CurrentDelay = InitialDelay;
            return;
        }

        var grown = CurrentDelay.Ticks * Multiplier;
        CurrentDelay = grown >= MaximumDelay.Ticks ? MaximumDelay : TimeSpan.FromTicks((long)grown);
    }

    /// <summary>Clears the failure history after a success; the next attempt is due immediately.</summary>
    public void Reset()
    {
        Failures = 0;
        CurrentDelay = TimeSpan.Zero;
    }
}
