using Sunmao.Core.Logging;
using Sunmao.Testing;

namespace Sunmao.Diagnostics.Tests;

public sealed class AsyncTextLogSinkTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SunmaoLogTests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Cleanup must not hide the test result.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task SparseEntriesBecomeVisibleWithoutFlushOrShutdown(int count)
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 9, 7, 15, 0, 0, TimeSpan.Zero));
        await using var sink = new AsyncTextLogSink(time);
        var destination = new LogFileDestination(_root, "test");
        Assert.True(sink.TryConfigure(destination));
        var path = destination.FilePath(new DateOnly(2026, 9, 7));

        // Two separated bursts exercise returning from idle as well as the initial drain.
        for (var burst = 0; burst < 2; burst++)
        {
            var marker = $"sparse-burst-{burst}-{count - 1}";
            for (var index = 0; index < count; index++)
            {
                sink.Write(LogLevel.Info, "test", $"sparse-burst-{burst}-{index}");
            }

            await TestWait.UntilAsync(
                () => File.Exists(path) && ReadAllTextShared(path).Contains(marker),
                because: "sparse traffic is flushed without a barrier");
        }

        Assert.Equal(0, sink.Snapshot.WriterFailed);
        Assert.Equal(0, sink.Snapshot.DroppedInfo);
    }

    [Fact]
    public async Task AcceptedEntriesAreFlushedToOneDailyFile()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        await using var sink = new AsyncTextLogSink(time, capacity: 32, criticalReserve: 4);
        var destination = new LogFileDestination(_root);

        Assert.True(sink.TryConfigure(destination));
        sink.Write(LogLevel.Info, "test", "first");
        sink.Write(LogLevel.Error, "test", "second", new InvalidOperationException("boom"));

        var flush = await sink.FlushAsync();

        Assert.True(flush.Succeeded, flush.Failure);
        var path = destination.FilePath(new DateOnly(2026, 1, 2));
        Assert.EndsWith("app-2026-01-02.log", path);
        var text = ReadAllTextShared(path);
        Assert.Contains("2026-01-02 03:04:05.000 [INFO] test | first", text);
        Assert.Contains("[ERROR] test | second", text);
        Assert.Contains("InvalidOperationException: boom", text);
        Assert.Equal(2, sink.Snapshot.Accepted);
        Assert.Equal(0, sink.Snapshot.WriterFailed);
    }

    [Fact]
    public async Task DestinationCanBeConfiguredAfterQueuePressureWithoutBlocking()
    {
        await using var sink = new AsyncTextLogSink(capacity: 8, criticalReserve: 2);

        for (var index = 0; index < 40; index++)
        {
            sink.Write(LogLevel.Info, "pressure", $"message-{index}");
        }

        Assert.True(sink.Snapshot.DroppedInfo > 0);
        Assert.True(sink.TryConfigure(_root));
        var flush = await sink.FlushAsync();

        Assert.True(flush.Succeeded, flush.Failure);
        Assert.True(sink.Snapshot.Accepted > 0);
        Assert.Equal(AsyncLogState.Ready, sink.Snapshot.State);
    }

    [Fact]
    public async Task WarningsAndErrorsUseTheCriticalReserve()
    {
        await using var sink = new AsyncTextLogSink(capacity: 8, criticalReserve: 4);

        for (var index = 0; index < 10; index++)
        {
            sink.Write(LogLevel.Info, "pressure", $"info-{index}");
        }

        sink.Write(LogLevel.Error, "pressure", "must be kept");

        Assert.Equal(6, sink.Snapshot.DroppedInfo);
        Assert.Equal(0, sink.Snapshot.DroppedError);
        Assert.Equal(5, sink.Snapshot.Accepted);
    }

    [Fact]
    public async Task DailyRotationUsesTheEntryLocalDate()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 2, 23, 59, 59, TimeSpan.Zero));
        await using var sink = new AsyncTextLogSink(time, capacity: 16, criticalReserve: 2);
        var destination = new LogFileDestination(_root);

        Assert.True(sink.TryConfigure(destination));
        sink.Write(LogLevel.Info, "rotation", "before-midnight");
        time.Advance(TimeSpan.FromSeconds(2));
        sink.Write(LogLevel.Info, "rotation", "after-midnight");

        var flush = await sink.FlushAsync();

        Assert.True(flush.Succeeded, flush.Failure);
        Assert.Contains("before-midnight", ReadAllTextShared(destination.FilePath(new DateOnly(2026, 1, 2))));
        Assert.Contains("after-midnight", ReadAllTextShared(destination.FilePath(new DateOnly(2026, 1, 3))));
    }

    [Fact]
    public async Task DisposeBeforeConfigurationDrainsWithoutHanging()
    {
        var sink = new AsyncTextLogSink(capacity: 8, criticalReserve: 2);
        sink.Write(LogLevel.Warning, "startup", "destination not selected yet");

        await sink.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(AsyncLogState.Stopped, sink.Snapshot.State);
        Assert.False(sink.Snapshot.IsAccepting);
        Assert.True(sink.Snapshot.WriterFailed >= 1);
        Assert.False(sink.TryConfigure(_root));
    }

    [Fact]
    public async Task FlushReportsWriterFailureInsteadOfClaimingSuccess()
    {
        Directory.CreateDirectory(_root);
        var blocked = Path.Combine(_root, "logs");
        await File.WriteAllTextAsync(blocked, "not a directory");
        await using var sink = new AsyncTextLogSink(capacity: 8, criticalReserve: 2);

        Assert.True(sink.TryConfigure(blocked));
        sink.Write(LogLevel.Error, "writer", "this cannot be persisted");

        var flush = await sink.FlushAsync();

        Assert.False(flush.Succeeded);
        Assert.NotNull(flush.Failure);
        Assert.True(sink.Snapshot.WriterFailed > 0);
        Assert.Equal(AsyncLogState.Degraded, sink.Snapshot.State);
    }

    [Fact]
    public async Task ConcurrentProducersPreserveEveryAcceptedEntry()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        await using var sink = new AsyncTextLogSink(time, capacity: 256, criticalReserve: 16);
        var destination = new LogFileDestination(_root);
        Assert.True(sink.TryConfigure(destination));

        var producers = Enumerable.Range(0, 8)
            .Select(worker => Task.Run(() =>
            {
                for (var index = 0; index < 25; index++)
                {
                    sink.Write(LogLevel.Warning, "concurrency", $"worker-{worker}-entry-{index}");
                }
            }))
            .ToArray();
        await Task.WhenAll(producers);

        var flush = await sink.FlushAsync();

        Assert.True(flush.Succeeded, flush.Failure);
        Assert.Equal(200, sink.Snapshot.Accepted);
        var text = ReadAllTextShared(destination.FilePath(new DateOnly(2026, 9, 1)));
        Assert.Contains("worker-0-entry-0", text);
        Assert.Contains("worker-7-entry-24", text);
    }

    [Fact]
    public async Task WritesAfterDisposeAreCountedNotThrown()
    {
        var sink = new AsyncTextLogSink();
        await sink.DisposeAsync();

        sink.Write(LogLevel.Error, "late", "after dispose");

        Assert.Equal(1, sink.Snapshot.RejectedAfterStop);
    }

    [Fact]
    public void InvalidDestinationsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new LogFileDestination(" "));
        Assert.Throws<ArgumentException>(() => new LogFileDestination(_root, "bad|prefix"));
    }

    private static string ReadAllTextShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
