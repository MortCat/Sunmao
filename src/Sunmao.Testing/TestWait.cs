using System.Diagnostics;

namespace Sunmao.Testing;

/// <summary>
/// Waits for a condition that becomes true asynchronously, for tests that observe snapshots or
/// state instead of subscribing to events.
/// </summary>
/// <remarks>
/// Use it instead of <c>Thread.Sleep</c> or fixed delays. Continuations stay on the caller's context,
/// so tests running under <see cref="SingleThreadUiTestHost"/> keep their thread affinity.
/// </remarks>
public static class TestWait
{
    /// <summary>Default time limit: 5 seconds.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Default polling period: 10 milliseconds.</summary>
    public static readonly TimeSpan DefaultPoll = TimeSpan.FromMilliseconds(10);

    /// <summary>Completes when <paramref name="condition"/> returns true.</summary>
    /// <param name="condition">Checked immediately, then once per <paramref name="poll"/>.</param>
    /// <param name="timeout">Time limit; defaults to <see cref="DefaultTimeout"/>.</param>
    /// <param name="poll">Polling period; defaults to <see cref="DefaultPoll"/>.</param>
    /// <param name="because">Explanation added to the timeout message.</param>
    /// <exception cref="TimeoutException">The condition stayed false for the whole time limit.</exception>
    public static async Task UntilAsync(
        Func<bool> condition,
        TimeSpan? timeout = null,
        TimeSpan? poll = null,
        string? because = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var limit = timeout ?? DefaultTimeout;
        var delay = poll ?? DefaultPoll;
        var started = Stopwatch.GetTimestamp();
        while (!condition())
        {
            if (Stopwatch.GetElapsedTime(started) > limit)
            {
                throw new TimeoutException(
                    $"Condition was not met within {limit.TotalMilliseconds:0} ms" +
                    (because is null ? "." : $": {because}"));
            }

            await Task.Delay(delay);
        }
    }
}
