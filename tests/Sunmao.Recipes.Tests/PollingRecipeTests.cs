using Sunmao.Core.Logging;
using Sunmao.Testing;

namespace Sunmao.Recipes.Tests;

public sealed class PollingRecipeTests
{
    [Fact]
    public async Task PollsOnlyAfterIntervalAndStopDrainsTimer()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        await using var owner = new PollingExample(time, NullLogSink.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.StartAsync(CancellationToken.None));
        await owner.InitializeAsync(CancellationToken.None);
        await owner.StartAsync(CancellationToken.None);
        await TestWait.UntilAsync(() => time.ActiveTimerCount == 1);
        Assert.Equal(0, owner.Count);
        time.Advance(TimeSpan.FromSeconds(1));
        await TestWait.UntilAsync(() => owner.Count == 1);
        await owner.StopAsync(CancellationToken.None);
        Assert.Equal(0, time.ActiveTimerCount);
        time.Advance(TimeSpan.FromDays(1));
        Assert.Equal(1, owner.Count);
    }
}
