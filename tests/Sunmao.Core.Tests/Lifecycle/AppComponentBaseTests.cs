using Sunmao.Core.Lifecycle;
using Sunmao.Core.Logging;
using Sunmao.Testing;

namespace Sunmao.Core.Tests.Lifecycle;

public sealed class AppComponentBaseTests
{
    [Fact]
    public async Task LifecycleMovesThroughTheExpectedStates()
    {
        await using var component = new TestComponent();
        var states = new List<ComponentState>();
        component.StateChanged += (_, args) => states.Add(args.Current);

        await component.InitializeAsync(CancellationToken.None);
        await component.StartAsync(CancellationToken.None);
        await component.StopAsync(CancellationToken.None);
        await component.StartAsync(CancellationToken.None);

        Assert.Equal(
            [
                ComponentState.Initializing, ComponentState.Ready,
                ComponentState.Starting, ComponentState.Running,
                ComponentState.Stopping, ComponentState.Stopped,
                ComponentState.Starting, ComponentState.Running
            ],
            states);
    }

    [Fact]
    public async Task StartBeforeInitializeIsRejected()
    {
        await using var component = new TestComponent();

        await Assert.ThrowsAsync<InvalidOperationException>(() => component.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DisabledComponentNeverStarts()
    {
        await using var component = new TestComponent(isEnabled: false);

        await component.InitializeAsync(CancellationToken.None);
        await component.StartAsync(CancellationToken.None);

        Assert.Equal(ComponentState.Disabled, component.State);
        Assert.DoesNotContain(component.Events, entry => entry == "start");
    }

    [Fact]
    public async Task StartFailureRunsCleanupBeforeFaultedAndLogsCleanupErrors()
    {
        var log = new RecordingLogSink();
        await using var component = new TestComponent(log: log)
        {
            StartHook = _ => throw new InvalidOperationException("start failed"),
            StartFailedHook = _ => throw new InvalidOperationException("cleanup failed")
        };
        await component.InitializeAsync(CancellationToken.None);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => component.StartAsync(CancellationToken.None));

        Assert.Equal("start failed", failure.Message);
        Assert.Contains("start-failed:Starting", component.Events);
        Assert.Equal(ComponentState.Faulted, component.State);
        Assert.Contains("Component startup cleanup failed.", log.Messages);
    }

    [Fact]
    public async Task ExclusiveOperationWaitsForAnInProgressStart()
    {
        var releaseStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var component = new TestComponent
        {
            StartHook = _ => releaseStart.Task
        };
        await component.InitializeAsync(CancellationToken.None);

        var start = component.StartAsync(CancellationToken.None);
        await TestWait.UntilAsync(() => component.State == ComponentState.Starting);
        var observedState = ComponentState.Created;
        var operation = component.RunAsync(_ =>
        {
            observedState = component.State;
            return Task.CompletedTask;
        });

        Assert.False(operation.IsCompleted);

        releaseStart.TrySetResult();
        await start;
        await operation;
        Assert.Equal(ComponentState.Running, observedState);
    }

    [Fact]
    public async Task ExclusiveOperationAfterDisposeThrowsUnlessAllowedWhileDisposing()
    {
        var component = new TestComponent();
        await component.InitializeAsync(CancellationToken.None);
        await component.StartAsync(CancellationToken.None);
        await component.DisposeAsync();

        var invoked = false;
        await Assert.ThrowsAsync<ObjectDisposedException>(() => component.RunAsync(_ =>
        {
            invoked = true;
            return Task.CompletedTask;
        }));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => component.RunAsync<int>(_ =>
        {
            invoked = true;
            return Task.FromResult(1);
        }));
        await component.RunAsync(
            _ =>
            {
                invoked = true;
                return Task.CompletedTask;
            },
            allowWhileDisposing: true);

        Assert.False(invoked);
        Assert.True(component.DisposeStarted);
        Assert.True(component.Disposed);
    }

    [Fact]
    public async Task StateHookRunsBeforeObserversAndObserverFailuresAreIsolated()
    {
        var log = new RecordingLogSink();
        await using var component = new TestComponent(log: log);
        component.StateChanged += (_, _) => throw new InvalidOperationException("observer failure");
        component.StateChanged += (_, args) => component.Record($"observer:{args.Current}");

        await component.InitializeAsync(CancellationToken.None);

        Assert.Equal(ComponentState.Ready, component.State);
        Assert.Equal(
            ["hook:Initializing", "observer:Initializing", "hook:Ready", "observer:Ready"],
            component.Events);
        Assert.Contains("A Component state observer failed.", log.Messages);
    }

    [Fact]
    public async Task StopFailureFaultsTheComponentAndDisposeStillReleasesResources()
    {
        var component = new TestComponent
        {
            StopHook = _ => throw new InvalidOperationException("stop failed")
        };
        await component.InitializeAsync(CancellationToken.None);
        await component.StartAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => component.StopAsync(CancellationToken.None));
        Assert.Equal(ComponentState.Faulted, component.State);

        // Disposal retries the stop, still releases resources, then surfaces the stop failure.
        await Assert.ThrowsAsync<InvalidOperationException>(() => component.DisposeAsync().AsTask());
        Assert.Contains("dispose", component.Events);
        Assert.True(component.Disposed);
    }

    [Fact]
    public async Task ConcurrentDisposeCallersShareOneDisposal()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var component = new TestComponent { DisposeHook = () => release.Task };
        await component.InitializeAsync(CancellationToken.None);

        var first = component.DisposeAsync().AsTask();
        var second = component.DisposeAsync().AsTask();
        Assert.False(second.IsCompleted);

        release.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(component.Events, entry => entry == "dispose");
    }

    private sealed class TestComponent(bool isEnabled = true, ILogSink? log = null)
        : AppComponentBase(isEnabled, log)
    {
        private readonly List<string> _events = [];

        public Func<CancellationToken, Task> StartHook { get; init; } = _ => Task.CompletedTask;

        public Func<CancellationToken, Task> StopHook { get; init; } = _ => Task.CompletedTask;

        public Func<Exception, Task> StartFailedHook { get; init; } = _ => Task.CompletedTask;

        public Func<Task> DisposeHook { get; init; } = () => Task.CompletedTask;

        public IReadOnlyList<string> Events
        {
            get
            {
                lock (_events)
                {
                    return _events.ToArray();
                }
            }
        }

        public bool DisposeStarted => IsDisposeStarted;

        public bool Disposed => IsDisposed;

        public void Record(string entry)
        {
            lock (_events)
            {
                _events.Add(entry);
            }
        }

        public Task RunAsync(Func<CancellationToken, Task> operation, bool allowWhileDisposing = false) =>
            RunExclusiveAsync(operation, CancellationToken.None, allowWhileDisposing);

        public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation) =>
            RunExclusiveAsync(operation, CancellationToken.None);

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            Record("start");
            return StartHook(cancellationToken);
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken) => StopHook(cancellationToken);

        protected override async Task OnStartFailedAsync(Exception startFailure)
        {
            Record($"start-failed:{State}");
            await StartFailedHook(startFailure);
        }

        protected override async ValueTask OnDisposeAsync()
        {
            await DisposeHook();
            Record("dispose");
        }

        protected override void OnStateChanged(ComponentStateChangedEventArgs args) =>
            Record($"hook:{args.Current}");
    }
}
