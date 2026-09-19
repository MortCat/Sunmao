using Sunmao.Communication;
using Sunmao.Core.Logging;
using Sunmao.Testing;

namespace Sunmao.Recipes.Tests;

public sealed class ConnectionRecipeTests
{
    [Fact]
    public async Task RecoversFromFailureAndDisposesOwnedTransport()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var transport = new RecoveringTransport();
        await using (var owner = new ConnectionExample(transport, time, NullLogSink.Instance))
        {
            await owner.InitializeAsync(CancellationToken.None);
            await owner.StartAsync(CancellationToken.None);
            await TestWait.UntilAsync(() => owner.Snapshot.ConsecutiveFailures == 1 && time.ActiveTimerCount == 1);
            Assert.Equal(ConnectionState.Disconnected, owner.Snapshot.State);
            time.Advance(TimeSpan.FromSeconds(1));
            await TestWait.UntilAsync(() => owner.Snapshot.IsConnected);
            Assert.Equal(0, owner.Snapshot.ConsecutiveFailures);
            await owner.StopAsync(CancellationToken.None);
            Assert.Equal(ConnectionState.Stopped, owner.Snapshot.State);
            Assert.False(transport.IsConnected);
            Assert.Equal(0, time.ActiveTimerCount);
        }
        Assert.True(transport.Disposed);
    }

    private sealed class RecoveringTransport : IByteTransport
    {
        private int _attempts;
        private int _connected;
        public string Endpoint => "memory://example";
        public bool IsConnected => Volatile.Read(ref _connected) == 1;
        public bool Disposed { get; private set; }
        public Task ConnectAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _attempts) == 1)
                throw new IOException("Injected connection failure.");
            Volatile.Write(ref _connected, 1);
            return Task.CompletedTask;
        }
        public Task DisconnectAsync()
        {
            Volatile.Write(ref _connected, 0);
            return Task.CompletedTask;
        }
        public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken token) =>
            throw new NotSupportedException("This recipe only exercises connection ownership.");
        public ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken token) =>
            throw new NotSupportedException("This recipe only exercises connection ownership.");
        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync();
            Disposed = true;
        }
    }
}
