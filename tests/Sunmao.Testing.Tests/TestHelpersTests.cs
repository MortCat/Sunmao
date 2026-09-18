using Sunmao.Core.Logging;

namespace Sunmao.Testing.Tests;

public sealed class TestHelpersTests
{
    [Fact]
    public async Task UntilAsyncCompletesOnceTheConditionHolds()
    {
        var calls = 0;

        await TestWait.UntilAsync(() => ++calls >= 3, poll: TimeSpan.FromMilliseconds(1));

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task UntilAsyncTimesOutWithTheReason()
    {
        var failure = await Assert.ThrowsAsync<TimeoutException>(() => TestWait.UntilAsync(
            () => false,
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(5),
            because: "the device never connects"));

        Assert.Contains("the device never connects", failure.Message);
    }

    [Fact]
    public async Task UiHostKeepsEveryContinuationOnItsOwnThread()
    {
        var testThread = Environment.CurrentManagedThreadId;
        int? hostThread = null;
        var threadsSeen = new HashSet<int>();

        await SingleThreadUiTestHost.RunAsync(async () =>
        {
            hostThread = Environment.CurrentManagedThreadId;
            Assert.Equal(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            for (var index = 0; index < 3; index++)
            {
                await Task.Delay(1);
                threadsSeen.Add(Environment.CurrentManagedThreadId);
            }
        });

        Assert.NotNull(hostThread);
        Assert.NotEqual(testThread, hostThread);
        Assert.Equal([hostThread.Value], threadsSeen);
    }

    [Fact]
    public async Task UiHostPropagatesScenarioFailures()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SingleThreadUiTestHost.RunAsync(() => throw new InvalidOperationException("scenario failed")));

        Assert.Equal("scenario failed", failure.Message);
    }

    [Fact]
    public void RecordingLogSinkKeepsEntriesInOrder()
    {
        var log = new RecordingLogSink();
        var error = new InvalidOperationException("boom");

        log.Write(LogLevel.Info, "a", "first");
        log.Write(LogLevel.Error, "b", "second", error);

        Assert.Equal(["first", "second"], log.Messages);
        Assert.Equal(new LogEntry(LogLevel.Error, "b", "second", error), log.Entries[1]);
    }
}
