using Sunmao.Core.Resilience;
using Sunmao.Testing;

namespace Sunmao.Core.Tests.Resilience;

public sealed class ExponentialBackoffTests
{
    [Fact]
    public void AttemptIsDueImmediatelyBeforeAnyFailure()
    {
        var backoff = new ExponentialBackoff(timeProvider: new ManualTimeProvider());

        Assert.True(backoff.IsAttemptDue);
        Assert.Equal(0, backoff.Failures);
        Assert.Equal(TimeSpan.Zero, backoff.TimeUntilNextAttempt);
    }

    [Fact]
    public void DelayStartsAtTheInitialValueDoublesAndCapsAtTheMaximum()
    {
        var time = new ManualTimeProvider();
        var backoff = new ExponentialBackoff(timeProvider: time);
        var delays = new List<TimeSpan>();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            backoff.RecordFailure();
            delays.Add(backoff.CurrentDelay);
        }

        Assert.Equal(
            [0.5, 1, 2, 4, 5, 5],
            delays.Select(delay => delay.TotalSeconds));
    }

    [Fact]
    public void NextAttemptBecomesDueWhenTheDelayHasElapsed()
    {
        var time = new ManualTimeProvider();
        var backoff = new ExponentialBackoff(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), timeProvider: time);

        backoff.RecordFailure();
        time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.False(backoff.IsAttemptDue);
        Assert.Equal(TimeSpan.FromMilliseconds(1), backoff.TimeUntilNextAttempt);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(backoff.IsAttemptDue);
    }

    [Fact]
    public void ResetMakesTheNextAttemptDueAndRestartsTheSequence()
    {
        var backoff = new ExponentialBackoff(timeProvider: new ManualTimeProvider());
        backoff.RecordFailure();
        backoff.RecordFailure();

        backoff.Reset();
        backoff.RecordFailure();

        Assert.Equal(1, backoff.Failures);
        Assert.Equal(ExponentialBackoff.DefaultInitialDelay, backoff.CurrentDelay);
    }

    [Fact]
    public void InvalidSettingsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialBackoff(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialBackoff(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExponentialBackoff(multiplier: 0.5));
    }
}
