using Sunmao.Core.Lifecycle;
using Sunmao.Core.Logging;
using Sunmao.Core.Polling;

namespace Sunmao.App;

/// <summary>
/// Example long-lived Component. Replace its simulated update with an application-owned device
/// operation after the real protocol and failure contract are known.
/// </summary>
public sealed class DeviceComponent : AppComponentBase
{
    private readonly TimeProvider _timeProvider;
    private readonly DevicePoller _poller;
    private DeviceSnapshot _snapshot;
    private long _sequence;

    /// <summary>Creates a stopped simulated Component.</summary>
    /// <param name="logSink">Receives poll failures.</param>
    /// <param name="timeProvider">Time source for snapshots and polling; defaults to the system.</param>
    public DeviceComponent(ILogSink? logSink = null, TimeProvider? timeProvider = null)
        : base(isEnabled: true, logSink)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _snapshot = new DeviceSnapshot(0, 0, _timeProvider.GetUtcNow());
        _poller = new DevicePoller(this, logSink, _timeProvider);
    }

    /// <summary>Latest immutable state, safe to read from any thread.</summary>
    public DeviceSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Runs one immediate update under the Component lifecycle gate.</summary>
    /// <param name="cancellationToken">Cancels waiting for the gate or the update.</param>
    public Task RefreshAsync(CancellationToken cancellationToken = default) =>
        RunExclusiveAsync(_ =>
        {
            PublishNextValue();
            return Task.CompletedTask;
        }, cancellationToken);

    /// <inheritdoc />
    protected override Task OnStartAsync(CancellationToken cancellationToken) =>
        _poller.StartAsync(cancellationToken);

    /// <inheritdoc />
    protected override Task OnStopAsync(CancellationToken cancellationToken) =>
        _poller.StopAsync();

    /// <inheritdoc />
    protected override ValueTask OnDisposeAsync() => _poller.DisposeAsync();

    private void PublishNextValue()
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var next = new DeviceSnapshot(sequence, Random.Shared.Next(0, 100), _timeProvider.GetUtcNow());
        var observed = Volatile.Read(ref _snapshot);
        if (observed.Sequence < next.Sequence)
        {
            Interlocked.CompareExchange(ref _snapshot, next, observed);
        }
    }

    private sealed class DevicePoller(DeviceComponent owner, ILogSink? logSink, TimeProvider timeProvider)
        : PollingTaskBase(
            "simulated-device",
            logSink,
            TimeSpan.FromMilliseconds(250),
            timeProvider,
            delayFirstPoll: true)
    {
        protected override ValueTask<PollingResult> PollAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            owner.PublishNextValue();
            return ValueTask.FromResult(PollingResult.Continue);
        }
    }
}
