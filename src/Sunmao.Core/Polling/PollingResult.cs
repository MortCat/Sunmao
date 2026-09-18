namespace Sunmao.Core.Polling;

/// <summary>Decision returned after one polling iteration.</summary>
public enum PollingDecision
{
    /// <summary>Keep polling.</summary>
    Continue,

    /// <summary>End the loop; the poller stops itself.</summary>
    Stop
}

/// <summary>
/// Result of one polling iteration, including an optional cadence change. A changed interval stays
/// in effect for later iterations until another result or <c>SetInterval</c> changes it.
/// </summary>
public readonly record struct PollingResult
{
    /// <summary>Creates a result.</summary>
    /// <param name="decision">Whether to keep polling.</param>
    /// <param name="nextInterval">New interval from now on, or null to keep the current one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="nextInterval"/> is not positive.</exception>
    public PollingResult(PollingDecision decision, TimeSpan? nextInterval = null)
    {
        if (nextInterval.HasValue && nextInterval.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextInterval),
                nextInterval,
                "Polling interval must be greater than zero.");
        }

        Decision = decision;
        NextInterval = nextInterval;
    }

    /// <summary>Whether to keep polling.</summary>
    public PollingDecision Decision { get; }

    /// <summary>New interval from now on, or null to keep the current one.</summary>
    public TimeSpan? NextInterval { get; }

    /// <summary>Keep polling at the current interval.</summary>
    public static PollingResult Continue { get; } = new(PollingDecision.Continue);

    /// <summary>End the loop.</summary>
    public static PollingResult Stop { get; } = new(PollingDecision.Stop);

    /// <summary>Keep polling and use <paramref name="interval"/> from now on.</summary>
    /// <param name="interval">New interval; must be positive.</param>
    public static PollingResult ContinueAfter(TimeSpan interval) =>
        new(PollingDecision.Continue, interval);
}

/// <summary>Decision made after a polling exception.</summary>
public readonly record struct PollingErrorDecision
{
    /// <summary>Creates a decision.</summary>
    /// <param name="decision">Whether to keep polling.</param>
    /// <param name="retryAfter">New interval from now on, or null to keep the current one.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="retryAfter"/> is not positive.</exception>
    public PollingErrorDecision(PollingDecision decision, TimeSpan? retryAfter = null)
    {
        if (retryAfter.HasValue && retryAfter.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryAfter),
                retryAfter,
                "Retry interval must be greater than zero.");
        }

        Decision = decision;
        RetryAfter = retryAfter;
    }

    /// <summary>Whether to keep polling.</summary>
    public PollingDecision Decision { get; }

    /// <summary>New interval from now on, or null to keep the current one.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>Keep polling at the current interval.</summary>
    public static PollingErrorDecision Continue { get; } = new(PollingDecision.Continue);

    /// <summary>End the loop.</summary>
    public static PollingErrorDecision Stop { get; } = new(PollingDecision.Stop);

    /// <summary>Keep polling and use <paramref name="interval"/> from now on.</summary>
    /// <param name="interval">New interval; must be positive.</param>
    public static PollingErrorDecision RetryAfterDelay(TimeSpan interval) =>
        new(PollingDecision.Continue, interval);
}
