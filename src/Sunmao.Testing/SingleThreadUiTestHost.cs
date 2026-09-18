using System.Collections.Concurrent;
using System.Runtime.Versioning;

namespace Sunmao.Testing;

/// <summary>
/// Runs an asynchronous test scenario on one dedicated STA thread with a minimal message pump, so
/// UI-affine code (WPF objects, UI polling timers, view models) sees production-like thread affinity.
/// </summary>
/// <remarks>
/// Every <c>await</c> inside the scenario resumes on the same thread. Code that blocks that thread
/// (<c>.Wait()</c>, <c>.Result</c>) deadlocks, exactly as it would on a real UI thread.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class SingleThreadUiTestHost
{
    /// <summary>Runs <paramref name="scenario"/> on a new STA thread and completes when it finishes.</summary>
    /// <param name="scenario">The test body.</param>
    /// <returns>A task that completes with the scenario's outcome, including its exception.</returns>
    public static Task RunAsync(Func<Task> scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The one dedicated thread this test host owns: an STA UI thread with its own message pump.
        var thread = new Thread(() =>
        {
            using var context = new PumpSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);
            context.Post(async _ =>
            {
                try
                {
                    await scenario().ConfigureAwait(true);
                    completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
                finally
                {
                    context.Complete();
                }
            }, null);
            context.Run();
        })
        {
            IsBackground = true,
            Name = "Sunmao test UI thread"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback callback, object? state)
        {
            ArgumentNullException.ThrowIfNull(callback);
            _queue.Add((callback, state));
        }

        public void Run()
        {
            foreach (var work in _queue.GetConsumingEnumerable())
            {
                work.Callback(work.State);
            }
        }

        public void Complete() => _queue.CompleteAdding();

        public void Dispose() => _queue.Dispose();
    }
}
