using Sunmao.Communication;
using Sunmao.Core.Lifecycle;
using Sunmao.Core.Logging;

namespace Sunmao.Recipes;

internal sealed class ConnectionExample : AppComponentBase
{
    private readonly ManagedConnection _connection;

    internal ConnectionExample(IByteTransport transport, TimeProvider time, ILogSink log) : base(true, log)
    {
        _connection = new ManagedConnection(transport, log,
            checkInterval: TimeSpan.FromSeconds(1),
            retryInitialDelay: TimeSpan.FromSeconds(1),
            timeProvider: time);
    }

    internal ConnectionSnapshot Snapshot => _connection.Snapshot;
    protected override Task OnStartAsync(CancellationToken token) => _connection.StartAsync(token);
    protected override Task OnStopAsync(CancellationToken token) => _connection.StopAsync();
    protected override ValueTask OnDisposeAsync() => _connection.DisposeAsync();
}
