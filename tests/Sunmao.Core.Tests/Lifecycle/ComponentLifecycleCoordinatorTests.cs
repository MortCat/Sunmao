using Sunmao.Core.Lifecycle;

namespace Sunmao.Core.Tests.Lifecycle;

public sealed class ComponentLifecycleCoordinatorTests
{
    [Fact]
    public async Task InitializesAndStartsInOrderThenStopsInReverseOrder()
    {
        var calls = new List<string>();
        await using var coordinator = new ComponentLifecycleCoordinator(
        [
            new RecordingComponent("first", calls),
            new RecordingComponent("second", calls)
        ]);

        await coordinator.InitializeAsync(CancellationToken.None);
        await coordinator.StartAsync(CancellationToken.None);
        await coordinator.StopAsync();

        Assert.Equal(
            ["first.initialize", "second.initialize", "first.start", "second.start", "second.stop", "first.stop"],
            calls);
    }

    [Fact]
    public async Task InitializationFailureStopsAlreadyInitializedComponentsAndNeverStartsAny()
    {
        var calls = new List<string>();
        var first = new RecordingComponent("first", calls);
        var second = new RecordingComponent("second", calls)
        {
            InitializeFailure = new InvalidOperationException("second unavailable")
        };
        await using var coordinator = new ComponentLifecycleCoordinator([first, second]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.InitializeAsync(CancellationToken.None));

        Assert.Equal("second unavailable", exception.Message);
        Assert.Equal(["first.initialize", "second.initialize", "second.stop", "first.stop"], calls);
    }

    [Fact]
    public async Task StopFailureDoesNotPreventOtherComponentsFromStopping()
    {
        var calls = new List<string>();
        var first = new RecordingComponent("first", calls);
        var second = new RecordingComponent("second", calls)
        {
            StopFailure = new InvalidOperationException("second stop failed")
        };
        await using var coordinator = new ComponentLifecycleCoordinator([first, second]);
        await coordinator.InitializeAsync(CancellationToken.None);
        await coordinator.StartAsync(CancellationToken.None);

        var exception = await Assert.ThrowsAsync<AggregateException>(() => coordinator.StopAsync());

        Assert.Contains(exception.InnerExceptions, item => item.Message == "second stop failed");
        Assert.Equal(["first.initialize", "second.initialize", "first.start", "second.start", "second.stop", "first.stop"], calls);

        // Let the async-using disposal take the normal path after the one-shot failure.
        second.StopFailure = null;
    }

    [Fact]
    public async Task DisposeStopsThenDisposesInReverseOrder()
    {
        var calls = new List<string>();
        var coordinator = new ComponentLifecycleCoordinator();
        coordinator.Register(new RecordingComponent("second", calls));
        coordinator.Register(new RecordingComponent("first", calls), first: true);
        await coordinator.InitializeAsync(CancellationToken.None);
        await coordinator.StartAsync(CancellationToken.None);
        calls.Clear();

        await coordinator.DisposeAsync();

        Assert.Equal(["second.stop", "first.stop", "second.dispose", "first.dispose"], calls);
    }

    [Fact]
    public async Task RegistrationAfterInitializationIsRejected()
    {
        await using var coordinator = new ComponentLifecycleCoordinator();
        await coordinator.InitializeAsync(CancellationToken.None);

        Assert.Throws<InvalidOperationException>(() => coordinator.Register(new RecordingComponent("late", [])));
    }

    private sealed class RecordingComponent(string name, List<string> calls) : AppComponentBase(isEnabled: true)
    {
        public Exception? InitializeFailure { get; set; }

        public Exception? StopFailure { get; set; }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            calls.Add($"{name}.initialize");
            return InitializeFailure is null ? Task.CompletedTask : throw InitializeFailure;
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken)
        {
            calls.Add($"{name}.start");
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken)
        {
            calls.Add($"{name}.stop");
            return StopFailure is null ? Task.CompletedTask : throw StopFailure;
        }

        protected override ValueTask OnDisposeAsync()
        {
            calls.Add($"{name}.dispose");
            return ValueTask.CompletedTask;
        }
    }
}
