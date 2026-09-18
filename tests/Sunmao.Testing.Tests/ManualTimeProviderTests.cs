namespace Sunmao.Testing.Tests;

public sealed class ManualTimeProviderTests
{
    [Fact]
    public void ClockOnlyMovesWhenAdvanced()
    {
        var start = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var time = new ManualTimeProvider(start);
        var timestamp = time.GetTimestamp();

        Assert.Equal(start, time.GetUtcNow());
        time.Advance(TimeSpan.FromSeconds(3));

        Assert.Equal(start.AddSeconds(3), time.GetUtcNow());
        Assert.Equal(TimeSpan.FromSeconds(3), time.GetElapsedTime(timestamp));
        Assert.Equal(TimeSpan.FromSeconds(3), time.Elapsed);
    }

    [Fact]
    public async Task DelayCompletesOnlyWhenItsDueTimeIsReached()
    {
        var time = new ManualTimeProvider();
        var delay = Task.Delay(TimeSpan.FromSeconds(1), time);

        time.Advance(TimeSpan.FromMilliseconds(999));
        Assert.False(delay.IsCompleted);

        time.Advance(TimeSpan.FromMilliseconds(1));
        await delay.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TimersFireInDueOrderAndSeeTheirOwnDueTime()
    {
        var time = new ManualTimeProvider();
        var fired = new List<(string Name, TimeSpan At)>();
        using var late = time.CreateTimer(_ => fired.Add(("late", time.Elapsed)), null, TimeSpan.FromSeconds(3), Timeout.InfiniteTimeSpan);
        using var early = time.CreateTimer(_ => fired.Add(("early", time.Elapsed)), null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);

        time.Advance(TimeSpan.FromSeconds(5));

        Assert.Equal([("early", TimeSpan.FromSeconds(1)), ("late", TimeSpan.FromSeconds(3))], fired);
        Assert.Equal(TimeSpan.FromSeconds(5), time.Elapsed);
        Assert.Equal(0, time.ActiveTimerCount);
    }

    [Fact]
    public void PeriodicTimerFiresOncePerPeriod()
    {
        var time = new ManualTimeProvider();
        var count = 0;
        using var timer = time.CreateTimer(_ => count++, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        time.Advance(TimeSpan.FromSeconds(3.5));

        Assert.Equal(3, count);
        Assert.Equal(1, time.ActiveTimerCount);
    }

    [Fact]
    public async Task CancelledDelayRemovesItsTimer()
    {
        var time = new ManualTimeProvider();
        using var cancellation = new CancellationTokenSource();
        var delay = Task.Delay(TimeSpan.FromSeconds(1), time, cancellation.Token);
        Assert.Equal(1, time.ActiveTimerCount);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delay);
        Assert.Equal(0, time.ActiveTimerCount);
    }

    [Fact]
    public void ChangeAndDisposeRescheduleOrStopATimer()
    {
        var time = new ManualTimeProvider();
        var count = 0;
        var timer = time.CreateTimer(_ => count++, null, TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);

        Assert.True(timer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan));
        time.Advance(TimeSpan.FromSeconds(4));
        Assert.Equal(0, count);

        timer.Dispose();
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, count);
        Assert.False(timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan));
    }

    [Fact]
    public void NegativeAdvanceIsRejected()
    {
        var time = new ManualTimeProvider();

        Assert.Throws<ArgumentOutOfRangeException>(() => time.Advance(TimeSpan.FromTicks(-1)));
    }
}
