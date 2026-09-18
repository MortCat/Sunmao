using Sunmao.Core.Logging;
using Sunmao.Core.Polling;
using Sunmao.Testing;

namespace Sunmao.Core.Tests.Polling;

public sealed class PollingTaskBaseTests
{
    [Fact]
    public async Task PollCanStopItselfWithoutAwaitingItsOwnLoop()
    {
        await using var poller = new ScriptedPoller([PollingResult.Stop], TimeSpan.FromMilliseconds(1));

        await poller.StartAsync();
        await poller.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await poller.StopAsync();

        Assert.Equal(1, poller.PollCount);
        Assert.False(poller.IsRunning);
        Assert.Equal(["start", "stop"], poller.Hooks);
    }

    [Fact]
    public async Task StartStopAndRestartAreIdempotent()
    {
        await using var poller = new ScriptedPoller(
            [PollingResult.Continue, PollingResult.Stop],
            TimeSpan.FromMilliseconds(1));

        await poller.StartAsync();
        await poller.StartAsync();
        await TestWait.UntilAsync(() => poller.PollCount >= 2);
        await poller.StopAsync();
        var firstRunCount = poller.PollCount;

        await poller.StartAsync();
        await TestWait.UntilAsync(() => poller.PollCount >= firstRunCount + 2);
        await poller.StopAsync();

        Assert.Equal(4, poller.PollCount);
        Assert.Equal(2, firstRunCount);
        Assert.False(poller.IsRunning);
        Assert.Equal(2, poller.StartHookCount);
        Assert.Equal(2, poller.StopHookCount);
    }

    [Fact]
    public async Task PollCanChangeTheNextCadence()
    {
        var faster = TimeSpan.FromMilliseconds(3);
        await using var poller = new ScriptedPoller(
            [PollingResult.ContinueAfter(faster), PollingResult.Stop],
            TimeSpan.FromMilliseconds(30));

        await poller.StartAsync();
        await poller.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(faster, poller.Interval);
        Assert.Equal(2, poller.PollCount);
    }

    [Fact]
    public async Task ChangedCadencePersistsAcrossLaterContinueResults()
    {
        var faster = TimeSpan.FromMilliseconds(3);
        await using var poller = new ScriptedPoller(
            [PollingResult.ContinueAfter(faster), PollingResult.Continue, PollingResult.Continue, PollingResult.Stop],
            TimeSpan.FromMilliseconds(30));

        await poller.StartAsync();
        await poller.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(4, poller.PollCount);
        Assert.Equal(faster, poller.Interval);
    }

    [Fact]
    public async Task ErrorPolicyCanBackoffAndThenStop()
    {
        var retry = TimeSpan.FromMilliseconds(2);
        var log = new RecordingLogSink();
        await using var poller = new ErrorPoller(retry, log);

        await poller.StartAsync();
        await poller.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(2, poller.PollCount);
        Assert.Equal(retry, poller.Interval);
        Assert.Equal(2, poller.ErrorCount);
        Assert.False(poller.IsRunning);
        Assert.Equal(2, log.Entries.Count(entry => entry.Level == LogLevel.Error));
    }

    [Fact]
    public async Task DisposeCancelsAndAwaitsAnInFlightPoll()
    {
        var pollEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pollReleased = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var poller = new BlockingPoller(pollEntered, pollReleased);

        await poller.StartAsync();
        await pollEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var disposeTask = poller.DisposeAsync().AsTask();

        pollReleased.TrySetResult(true);
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(poller.PollCancellationObserved);
        Assert.False(poller.IsRunning);
    }

    [Fact]
    public async Task StartAfterDisposeIsRejected()
    {
        var poller = new ScriptedPoller([PollingResult.Stop]);
        await poller.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => poller.StartAsync());
    }

    [Fact]
    public async Task StoppingHookTokenDistinguishesExternalStopFromSelfStop()
    {
        await using var external = new ScriptedPoller([PollingResult.Continue], TimeSpan.FromMilliseconds(1));
        await external.StartAsync();
        await TestWait.UntilAsync(() => external.PollCount >= 1);
        await external.StopAsync();
        Assert.True(external.StoppingTokenWasCancelled);

        await using var self = new ScriptedPoller([PollingResult.Stop], TimeSpan.FromMilliseconds(1));
        await self.StartAsync();
        await self.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(self.StoppingTokenWasCancelled);
    }

    [Fact]
    public async Task FirstPollRunsImmediatelyByDefault()
    {
        var time = new ManualTimeProvider();
        await using var poller = new ScriptedPoller([PollingResult.Continue], TimeSpan.FromSeconds(1), time);

        await poller.StartAsync();

        await TestWait.UntilAsync(() => poller.PollCount == 1, because: "the first poll does not wait");
        Assert.Equal(TimeSpan.Zero, time.Elapsed);
    }

    [Fact]
    public async Task DelayFirstPollWaitsOneIntervalThenKeepsTheCadence()
    {
        var interval = TimeSpan.FromSeconds(1);
        var time = new ManualTimeProvider();
        await using var poller = new ScriptedPoller(
            [PollingResult.Continue],
            interval,
            time,
            delayFirstPoll: true);

        await poller.StartAsync();
        await TestWait.UntilAsync(() => time.ActiveTimerCount == 1, because: "the loop schedules its first delay");
        time.Advance(interval - TimeSpan.FromTicks(1));
        Assert.Equal(0, poller.PollCount);

        time.Advance(TimeSpan.FromTicks(1));
        await TestWait.UntilAsync(() => poller.PollCount == 1, because: "one interval has elapsed");

        // The second poll must wait a full interval after the first, not run back to back.
        await TestWait.UntilAsync(() => time.ActiveTimerCount == 1, because: "the loop schedules its next delay");
        Assert.Equal(1, poller.PollCount);
        time.Advance(interval);
        await TestWait.UntilAsync(() => poller.PollCount == 2, because: "a second interval has elapsed");
    }

    [Fact]
    public async Task StopDuringTheFirstDelayRunsTheStoppingHookWithoutPolling()
    {
        var time = new ManualTimeProvider();
        await using var poller = new ScriptedPoller(
            [PollingResult.Continue],
            TimeSpan.FromSeconds(1),
            time,
            delayFirstPoll: true);

        await poller.StartAsync();
        await TestWait.UntilAsync(() => time.ActiveTimerCount == 1);
        await poller.StopAsync();

        Assert.Equal(0, poller.PollCount);
        Assert.Equal(["start", "stop"], poller.Hooks);
        Assert.True(poller.StoppingTokenWasCancelled);
    }

    [Fact]
    public void NonPositiveIntervalsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScriptedPoller([PollingResult.Stop], TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => PollingResult.ContinueAfter(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => PollingErrorDecision.RetryAfterDelay(TimeSpan.FromTicks(-1)));
    }

    private sealed class ScriptedPoller : PollingTaskBase
    {
        private readonly PollingResult[] _results;
        private readonly List<string> _hooks = [];
        private int _resultIndex;
        private int _pollCount;
        private int _startHookCount;
        private int _stopHookCount;
        private int _stoppingTokenCancelled = -1;

        public ScriptedPoller(
            IEnumerable<PollingResult> results,
            TimeSpan? interval = null,
            TimeProvider? timeProvider = null,
            bool delayFirstPoll = false)
            : base("scripted", interval: interval, timeProvider: timeProvider, delayFirstPoll: delayFirstPoll)
        {
            _results = results.ToArray();
        }

        public TaskCompletionSource<bool> Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<string> Hooks
        {
            get
            {
                lock (_hooks)
                {
                    return _hooks.ToArray();
                }
            }
        }

        public int PollCount => Volatile.Read(ref _pollCount);

        public int StartHookCount => Volatile.Read(ref _startHookCount);

        public int StopHookCount => Volatile.Read(ref _stopHookCount);

        public bool StoppingTokenWasCancelled => Volatile.Read(ref _stoppingTokenCancelled) == 1;

        protected override ValueTask<PollingResult> PollAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _pollCount);
            var index = Math.Min(Interlocked.Increment(ref _resultIndex) - 1, _results.Length - 1);
            return ValueTask.FromResult(_results[index]);
        }

        protected override Task OnStartingAsync(CancellationToken cancellationToken)
        {
            AddHook("start");
            Interlocked.Increment(ref _startHookCount);
            Interlocked.Exchange(ref _resultIndex, 0);
            return Task.CompletedTask;
        }

        protected override Task OnStoppingAsync(CancellationToken cancellationToken)
        {
            AddHook("stop");
            Interlocked.Increment(ref _stopHookCount);
            Volatile.Write(ref _stoppingTokenCancelled, cancellationToken.IsCancellationRequested ? 1 : 0);
            Stopped.TrySetResult(true);
            return Task.CompletedTask;
        }

        private void AddHook(string hook)
        {
            lock (_hooks)
            {
                _hooks.Add(hook);
            }
        }
    }

    private sealed class ErrorPoller(TimeSpan retry, ILogSink log)
        : PollingTaskBase("error", log, TimeSpan.FromMilliseconds(30))
    {
        private int _pollCount;
        private int _errorCount;

        public TaskCompletionSource<bool> Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int PollCount => Volatile.Read(ref _pollCount);

        public int ErrorCount => Volatile.Read(ref _errorCount);

        protected override ValueTask<PollingResult> PollAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _pollCount);
            throw new InvalidOperationException("expected test error");
        }

        protected override ValueTask<PollingErrorDecision> OnPollingErrorAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            var errors = Interlocked.Increment(ref _errorCount);
            return ValueTask.FromResult(errors == 1
                ? PollingErrorDecision.RetryAfterDelay(retry)
                : PollingErrorDecision.Stop);
        }

        protected override Task OnStoppingAsync(CancellationToken cancellationToken)
        {
            Stopped.TrySetResult(true);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingPoller(
        TaskCompletionSource<bool> entered,
        TaskCompletionSource<bool> released)
        : PollingTaskBase("blocking", interval: TimeSpan.FromMilliseconds(1))
    {
        private int _cancellationObserved;

        public bool PollCancellationObserved => Volatile.Read(ref _cancellationObserved) == 1;

        protected override async ValueTask<PollingResult> PollAsync(CancellationToken cancellationToken)
        {
            entered.TrySetResult(true);
            try
            {
                await released.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Volatile.Write(ref _cancellationObserved, 1);
                throw;
            }

            return PollingResult.Continue;
        }
    }
}
