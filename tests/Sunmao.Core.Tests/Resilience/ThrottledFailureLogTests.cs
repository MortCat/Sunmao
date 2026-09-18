using Sunmao.Core.Logging;
using Sunmao.Core.Resilience;
using Sunmao.Testing;

namespace Sunmao.Core.Tests.Resilience;

public sealed class ThrottledFailureLogTests
{
    [Fact]
    public void FirstFailureIsWrittenThenRepeatsAreSummarizedPerInterval()
    {
        var time = new ManualTimeProvider();
        var log = new RecordingLogSink();
        var throttle = new ThrottledFailureLog(log, "device", TimeSpan.FromSeconds(60), time);

        Assert.True(throttle.RecordFailure("Device 'pump-a' is not connected"));
        time.Advance(TimeSpan.FromSeconds(30));
        Assert.False(throttle.RecordFailure("Device 'pump-a' is not connected"));
        time.Advance(TimeSpan.FromSeconds(30));
        Assert.True(throttle.RecordFailure("Device 'pump-a' is not connected"));

        Assert.Equal(
            [
                "Device 'pump-a' is not connected",
                "Device 'pump-a' is not connected (still failing after 3 attempts)"
            ],
            log.Messages);
        Assert.All(log.Entries, entry => Assert.Equal(LogLevel.Warning, entry.Level));
        Assert.All(log.Entries, entry => Assert.Equal("device", entry.Category));
    }

    [Fact]
    public void RecoveryIsLoggedOnceAndResetsTheCount()
    {
        var log = new RecordingLogSink();
        var throttle = new ThrottledFailureLog(log, "device", timeProvider: new ManualTimeProvider());
        throttle.RecordFailure("down");
        throttle.RecordFailure("down");

        Assert.True(throttle.RecordRecovery("Device 'pump-a' reconnected"));
        Assert.False(throttle.RecordRecovery("Device 'pump-a' reconnected"));
        Assert.True(throttle.RecordFailure("down again"));

        Assert.Equal(
            ["down", "Device 'pump-a' reconnected (after 2 failed attempts)", "down again"],
            log.Messages);
        Assert.Equal(LogLevel.Info, log.Entries[1].Level);
    }

    [Fact]
    public void RecoveryWithoutFailuresWritesNothing()
    {
        var log = new RecordingLogSink();
        var throttle = new ThrottledFailureLog(log, "device");

        Assert.False(throttle.RecordRecovery("up"));
        Assert.Empty(log.Entries);
    }
}
