using Sunmao.Core.Lifecycle;
using Sunmao.Core.Logging;
using Sunmao.Core.Polling;

namespace Sunmao.Recipes;

internal sealed class PollingExample : AppComponentBase
{
    private readonly Worker _worker;
    private long _count;

    internal PollingExample(TimeProvider time, ILogSink log) : base(true, log)
    {
        _worker = new Worker(this, time, log);
    }

    internal long Count => Interlocked.Read(ref _count);
    protected override Task OnStartAsync(CancellationToken token) => _worker.StartAsync(token);
    protected override Task OnStopAsync(CancellationToken token) => _worker.StopAsync();
    protected override ValueTask OnDisposeAsync() => _worker.DisposeAsync();

    private sealed class Worker(PollingExample owner, TimeProvider time, ILogSink log)
        : PollingTaskBase("example", log, TimeSpan.FromSeconds(1), time, delayFirstPoll: true)
    {
        protected override ValueTask<PollingResult> PollAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Interlocked.Increment(ref owner._count);
            return ValueTask.FromResult(PollingResult.Continue);
        }
    }
}
