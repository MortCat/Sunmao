using System.IO;
using Sunmao.Core.Logging;
using Sunmao.Testing;
using Sunmao.Wpf.Polling;

namespace Sunmao.Wpf.Tests;

public sealed class UiPollingTests
{
    [Fact]
    public void OnlyEnabledSubscriptionsReceivePulsesAndDisposeIsIsolated()
    {
        using var driver = new ManualUiPulseDriver();
        var first = 0;
        var second = 0;
        using var firstSubscription = driver.Register("first", _ => first++);
        using var secondSubscription = driver.Register("second", _ => second++);

        firstSubscription.Enable();
        driver.Start();
        driver.Pulse();
        secondSubscription.Enable();
        driver.Pulse();
        firstSubscription.Disable();
        driver.Pulse();
        secondSubscription.Dispose();
        driver.Pulse();

        Assert.Equal(2, first);
        Assert.Equal(2, second);
        Assert.False(firstSubscription.IsEnabled);
    }

    [Fact]
    public void PulseSequenceIsMonotonicAndStopKeepsRegistrations()
    {
        using var driver = new ManualUiPulseDriver();
        var sequences = new List<long>();
        using var subscription = driver.Register("sequence", pulse => sequences.Add(pulse.Sequence));

        subscription.Enable();
        driver.Start();
        driver.Pulse();
        driver.Stop();
        driver.Pulse();
        driver.Start();
        driver.Pulse();

        Assert.Equal([1L, 2L], sequences);
        Assert.True(subscription.IsEnabled);
    }

    [Fact]
    public void CallbackExceptionIsIsolatedAndLoggedWithTheOwner()
    {
        var log = new RecordingLogSink();
        using var driver = new ManualUiPulseDriver(log);
        var healthyCount = 0;
        using var failing = driver.Register("failing-page", _ => throw new InvalidOperationException("boom"));
        using var healthy = driver.Register("healthy-page", _ => healthyCount++);
        failing.Enable();
        healthy.Enable();
        driver.Start();

        driver.Pulse();
        driver.Pulse();

        Assert.Equal(2, healthyCount);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("ui-polling:failing-page", entry.Category);
    }

    [Fact]
    public void NestedPulseIsIgnoredInsteadOfRecursing()
    {
        using var driver = new ManualUiPulseDriver();
        var count = 0;
        using var subscription = driver.Register("nested", _ =>
        {
            count++;
            driver.Pulse();
        });
        subscription.Enable();
        driver.Start();

        driver.Pulse();

        Assert.Equal(1, count);
    }

    [Fact]
    public void RegistrationIsAffineToTheCreatingThread()
    {
        using var driver = new ManualUiPulseDriver();
        Exception? exception = null;
        var thread = new Thread(() => exception = Record.Exception(() => driver.Register("wrong-thread", _ => { })));
        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(exception);
    }

    [Fact]
    public void WrongThreadDisposeDoesNotDestroyTheOnlySubscriptionHandle()
    {
        using var driver = new ManualUiPulseDriver();
        var pulses = 0;
        var subscription = driver.Register("retry-dispose", _ => pulses++);
        subscription.Enable();
        driver.Start();

        Exception? exception = null;
        var thread = new Thread(() => exception = Record.Exception(subscription.Dispose));
        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(exception);
        driver.Pulse();
        Assert.Equal(1, pulses);

        subscription.Dispose();
        driver.Pulse();
        Assert.Equal(1, pulses);
    }

    [Fact]
    public void ThrowingLogSinkDoesNotBreakCallbackIsolation()
    {
        using var driver = new ManualUiPulseDriver(new ThrowingLogSink());
        var healthyCount = 0;
        using var failing = driver.Register("failing", _ => throw new InvalidOperationException("boom"));
        using var healthy = driver.Register("healthy", _ => healthyCount++);
        failing.Enable();
        healthy.Enable();
        driver.Start();

        var exception = Record.Exception(() => driver.Pulse());

        Assert.Null(exception);
        Assert.Equal(1, healthyCount);
    }

    [Fact]
    public void InactiveSourceNeverPulsesButTracksEnablement()
    {
        using var subscription = InactiveUiPollingSource.Instance.Register("headless", _ => throw new InvalidOperationException());

        subscription.Enable();
        Assert.True(subscription.IsEnabled);
        subscription.Dispose();
        Assert.False(subscription.IsEnabled);
    }

    [Fact]
    public Task WpfTimerPulsesOnTheUiThreadAndCountsLifecycle() =>
        SingleThreadUiTestHost.RunAsync(async () =>
        {
            var uiThread = Environment.CurrentManagedThreadId;
            using var timer = new UiPollingTimer(hertz: 50);
            var pulseThreads = new List<int>();
            using var subscription = timer.Register("probe", _ => pulseThreads.Add(Environment.CurrentManagedThreadId));
            subscription.Enable();

            timer.Start();
            timer.Start();
            await PumpUntilAsync(() => pulseThreads.Count >= 2);
            timer.Stop();

            Assert.All(pulseThreads, thread => Assert.Equal(uiThread, thread));
            Assert.Equal(1, timer.StartCount);
            Assert.Equal(1, timer.StopCount);
        });

    /// <summary>Runs the WPF dispatcher queue while waiting, because the test host pump does not.</summary>
    private static async Task PumpUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The UI pulse did not arrive.");
            }

            var frame = new System.Windows.Threading.DispatcherFrame();
            _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            await Task.Delay(5);
        }
    }

    private sealed class ThrowingLogSink : ILogSink
    {
        public void Write(LogLevel level, string category, string message, Exception? exception = null) =>
            throw new IOException("diagnostic sink is unavailable");
    }
}
