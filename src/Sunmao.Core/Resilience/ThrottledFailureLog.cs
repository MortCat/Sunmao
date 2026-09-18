using Sunmao.Core.Logging;

namespace Sunmao.Core.Resilience;

/// <summary>
/// Logs a repeating failure without flooding the log: the first failure is written immediately,
/// then at most one summary per interval while it keeps failing, and one entry on recovery.
/// </summary>
/// <remarks>
/// Pair it with <see cref="ExponentialBackoff"/> for reconnect loops. Not thread-safe: use it from
/// a single owner loop.
/// </remarks>
public sealed class ThrottledFailureLog
{
    /// <summary>Default interval between summaries: 60 seconds.</summary>
    public static readonly TimeSpan DefaultSummaryInterval = TimeSpan.FromSeconds(60);

    private readonly ILogSink _log;
    private readonly string _category;
    private readonly TimeProvider _timeProvider;
    private long _lastWrittenTimestamp;

    /// <summary>Creates a throttle with no recorded failures.</summary>
    /// <param name="log">Destination sink.</param>
    /// <param name="category">Log category for every entry.</param>
    /// <param name="summaryInterval">Minimum time between summaries; defaults to <see cref="DefaultSummaryInterval"/>.</param>
    /// <param name="timeProvider">Time source; defaults to the system.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="summaryInterval"/> is not positive.</exception>
    public ThrottledFailureLog(
        ILogSink log,
        string category,
        TimeSpan? summaryInterval = null,
        TimeProvider? timeProvider = null)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _category = string.IsNullOrWhiteSpace(category)
            ? throw new ArgumentException("A log category is required.", nameof(category))
            : category;
        SummaryInterval = summaryInterval ?? DefaultSummaryInterval;
        if (SummaryInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(summaryInterval), SummaryInterval, "The summary interval must be positive.");
        }

        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Minimum time between summary entries.</summary>
    public TimeSpan SummaryInterval { get; }

    /// <summary>Consecutive failures since construction or the last recovery.</summary>
    public int FailureCount { get; private set; }

    /// <summary>
    /// Records one failure. Writes <paramref name="message"/> for the first failure, and a summary
    /// with the failure count when <see cref="SummaryInterval"/> has passed since the last entry.
    /// </summary>
    /// <param name="message">What failed, for example "Device 'pump-a' is not connected".</param>
    /// <param name="exception">Optional failure details.</param>
    /// <param name="level">Severity; defaults to <see cref="LogLevel.Warning"/>.</param>
    /// <returns>True when an entry was written.</returns>
    public bool RecordFailure(string message, Exception? exception = null, LogLevel level = LogLevel.Warning)
    {
        FailureCount++;
        var now = _timeProvider.GetTimestamp();
        if (FailureCount == 1)
        {
            _lastWrittenTimestamp = now;
            _log.Write(level, _category, message, exception);
            return true;
        }

        if (_timeProvider.GetElapsedTime(_lastWrittenTimestamp, now) < SummaryInterval)
        {
            return false;
        }

        _lastWrittenTimestamp = now;
        _log.Write(level, _category, $"{message} (still failing after {FailureCount} attempts)", exception);
        return true;
    }

    /// <summary>
    /// Records a recovery. Writes one <see cref="LogLevel.Info"/> entry when failures were recorded,
    /// then clears the count.
    /// </summary>
    /// <param name="message">What recovered, for example "Device 'pump-a' reconnected".</param>
    /// <returns>True when an entry was written.</returns>
    public bool RecordRecovery(string message)
    {
        if (FailureCount == 0)
        {
            return false;
        }

        var failures = FailureCount;
        FailureCount = 0;
        _log.Write(LogLevel.Info, _category, $"{message} (after {failures} failed attempts)");
        return true;
    }
}
