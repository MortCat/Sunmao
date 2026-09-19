using System.Windows.Threading;
using Sunmao.App;
using Sunmao.Core.Lifecycle;
using Sunmao.Testing;
using Sunmao.Wpf.Polling;
using Xunit;

namespace Sunmao.App.Tests;

public sealed class DeviceComponentTests
{
    [Fact]
    public async Task ComponentStartsPollsAndStopsWithoutHardware()
    {
        var time = new ManualTimeProvider();
        await using var component = new DeviceComponent(timeProvider: time);
        await component.InitializeAsync(CancellationToken.None);
        await component.StartAsync(CancellationToken.None);

        await TestWait.UntilAsync(() => time.ActiveTimerCount > 0);
        time.Advance(TimeSpan.FromMilliseconds(250));
        await TestWait.UntilAsync(() => component.Snapshot.Sequence == 1);

        Assert.Equal(ComponentState.Running, component.State);
        await component.StopAsync(CancellationToken.None);
        Assert.Equal(ComponentState.Stopped, component.State);
    }

    [Fact]
    public async Task RefreshRunsThroughTheComponentContract()
    {
        await using var component = new DeviceComponent();
        await component.InitializeAsync(CancellationToken.None);
        await component.RefreshAsync();

        Assert.Equal(1, component.Snapshot.Sequence);
    }

    [Fact]
    public async Task ViewModelDisposalIsIdempotent()
    {
        await using var component = new DeviceComponent();
        using var pulse = new ManualUiPulseDriver();
        var viewModel = new DeviceViewModel(component, pulse);

        viewModel.Activate();
        await viewModel.DisposeAsync();
        await viewModel.DisposeAsync();

        Assert.False(viewModel.RefreshCommand.IsExecuting);
    }

    [Fact]
    public Task WindowDefersCloseUntilApplicationCleanupCompletes() =>
        SingleThreadUiTestHost.RunAsync(async () =>
        {
            await using var component = new DeviceComponent();
            using var pulse = new ManualUiPulseDriver();
            var shutdownStarted = NewSignal();
            var releaseShutdown = NewSignal();
            var windowClosed = NewSignal();

            var window = new MainWindow(
                component,
                pulse,
                async () =>
                {
                    shutdownStarted.TrySetResult();
                    await releaseShutdown.Task;
                });
            window.Closed += (_, _) => windowClosed.TrySetResult();

            window.Show();
            window.Close();

            await shutdownStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(window.IsVisible);

            releaseShutdown.TrySetResult();
            await Task.Yield();
            await PumpUntilAsync(() => windowClosed.Task.IsCompleted);
            Assert.False(window.IsVisible);
        });

    private static async Task PumpUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException("The WPF window did not close.");
            }

            var frame = new DispatcherFrame();
            var stopFrame = Dispatcher.CurrentDispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            await stopFrame.Task;
            await Task.Yield();
        }
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
