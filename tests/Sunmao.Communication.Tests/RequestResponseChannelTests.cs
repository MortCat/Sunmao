using System.Collections.Concurrent;
using System.Text;
using Sunmao.Testing;

namespace Sunmao.Communication.Tests;

public sealed class RequestResponseChannelTests
{
    [Fact]
    public async Task TimeoutResetsPartialFrameBeforeTheNextExchange()
    {
        var time = new ManualTimeProvider();
        await using var transport = new ControlledTransport();
        await transport.ConnectAsync(CancellationToken.None);
        transport.QueueResponse("STA"u8.ToArray());

        using var channel = new RequestResponseChannel(
            transport,
            DelimiterFrameDecoder.CarriageReturn(),
            timeProvider: time);
        var abandoned = channel.SendAsync("OLD\r"u8.ToArray(), TimeSpan.FromSeconds(1));

        await transport.SendStarted.WaitAsync(TimeSpan.FromSeconds(5));
        await transport.ResponseDelivered.WaitAsync(TimeSpan.FromSeconds(5));
        await TestWait.UntilAsync(() => time.ActiveTimerCount > 0);
        time.Advance(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAsync<TimeoutException>(() => abandoned);
        Assert.Equal(1, transport.DisconnectCount);

        await transport.ConnectAsync(CancellationToken.None);
        transport.QueueResponse("NEW\r"u8.ToArray());

        var response = await channel.SendAsync("NEXT\r"u8.ToArray(), TimeSpan.FromSeconds(1));

        Assert.Equal("NEW", Encoding.ASCII.GetString(response));
    }

    [Fact]
    public async Task CancellationWhileQueuedDoesNotSendOrDisconnect()
    {
        await using var transport = new ControlledTransport(holdFirstSend: true);
        await transport.ConnectAsync(CancellationToken.None);
        using var channel = new RequestResponseChannel(transport, DelimiterFrameDecoder.CarriageReturn());
        using var firstCancellation = new CancellationTokenSource();
        using var queuedCancellation = new CancellationTokenSource();

        var first = channel.SendAsync("FIRST\r"u8.ToArray(), TimeSpan.FromSeconds(5), firstCancellation.Token);
        await transport.SendStarted.WaitAsync(TimeSpan.FromSeconds(5));

        var queued = channel.SendAsync("QUEUED\r"u8.ToArray(), TimeSpan.FromSeconds(5), queuedCancellation.Token);
        queuedCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.Equal(1, transport.SendCount);
        Assert.Equal(0, transport.DisconnectCount);

        firstCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(1, transport.DisconnectCount);
    }

    [Fact]
    public async Task CancellationDuringExchangeDisconnectsTheTransport()
    {
        await using var transport = new ControlledTransport();
        await transport.ConnectAsync(CancellationToken.None);
        using var channel = new RequestResponseChannel(transport, DelimiterFrameDecoder.CarriageReturn());
        using var cancellation = new CancellationTokenSource();

        var request = channel.SendAsync("CANCEL\r"u8.ToArray(), TimeSpan.FromSeconds(5), cancellation.Token);
        await transport.SendStarted.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);

        Assert.Equal(1, transport.DisconnectCount);
        Assert.False(transport.IsConnected);
    }

    [Fact]
    public async Task ConcurrentCallersAreSerializedUntilEachResponseArrives()
    {
        await using var transport = new ControlledTransport(holdFirstSend: true)
        {
            ResponseFactory = request =>
                Encoding.ASCII.GetBytes($"ACK:{Encoding.ASCII.GetString(request)}\r")
        };
        await transport.ConnectAsync(CancellationToken.None);
        using var channel = new RequestResponseChannel(transport, DelimiterFrameDecoder.CarriageReturn());

        var first = channel.SendAsync("A\r"u8.ToArray());
        await transport.SendStarted.WaitAsync(TimeSpan.FromSeconds(5));
        var second = channel.SendAsync("B\r"u8.ToArray());

        await Task.Yield();
        Assert.Equal(1, transport.SendCount);
        Assert.False(second.IsCompleted);

        transport.ReleaseFirstSend();
        var responses = await Task.WhenAll(first, second);

        Assert.Equal(["ACK:A", "ACK:B"], responses.Select(Encoding.ASCII.GetString));
        Assert.Equal(1, transport.MaximumConcurrentSends);
    }

    private sealed class ControlledTransport(bool holdFirstSend = false) : IByteTransport
    {
        private readonly ConcurrentQueue<byte[]> _responses = new();
        private readonly SemaphoreSlim _responseSignal = new(0);
        private readonly TaskCompletionSource<bool> _firstSendRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _sendStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _responseDelivered =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _connected;
        private int _disconnectCount;
        private int _sendCount;
        private int _activeSends;
        private int _maximumConcurrentSends;

        public Func<byte[], byte[]?>? ResponseFactory { get; set; }

        public Task SendStarted => _sendStarted.Task;

        public Task ResponseDelivered => _responseDelivered.Task;

        public string Endpoint => "test://controlled";

        public bool IsConnected => Volatile.Read(ref _connected) != 0;

        public int DisconnectCount => Volatile.Read(ref _disconnectCount);

        public int SendCount => Volatile.Read(ref _sendCount);

        public int MaximumConcurrentSends => Volatile.Read(ref _maximumConcurrentSends);

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Volatile.Write(ref _connected, 1);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            Volatile.Write(ref _connected, 0);
            Interlocked.Increment(ref _disconnectCount);
            while (_responses.TryDequeue(out _))
            {
            }

            while (_responseSignal.Wait(0))
            {
            }

            return Task.CompletedTask;
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsConnected)
            {
                throw new IOException("Test transport is disconnected.");
            }

            var active = Interlocked.Increment(ref _activeSends);
            InterlockedMax(ref _maximumConcurrentSends, active);
            Interlocked.Increment(ref _sendCount);
            _sendStarted.TrySetResult(true);
            try
            {
                if (holdFirstSend && SendCount == 1)
                {
                    await _firstSendRelease.Task.WaitAsync(cancellationToken);
                }

                var response = ResponseFactory?.Invoke(data.ToArray());
                if (response is not null)
                {
                    QueueResponse(response);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activeSends);
            }
        }

        public async ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await _responseSignal.WaitAsync(cancellationToken);
            if (!_responses.TryDequeue(out var response))
            {
                throw new InvalidOperationException("The response signal was not paired with a response.");
            }

            response.AsMemory().CopyTo(buffer);
            _responseDelivered.TrySetResult(true);
            return response.Length;
        }

        public ValueTask DisposeAsync()
        {
            _responseSignal.Dispose();
            return ValueTask.CompletedTask;
        }

        public void QueueResponse(byte[] response)
        {
            _responses.Enqueue(response);
            _responseSignal.Release();
        }

        public void ReleaseFirstSend() => _firstSendRelease.TrySetResult(true);

        private static void InterlockedMax(ref int location, int value)
        {
            while (true)
            {
                var current = Volatile.Read(ref location);
                if (current >= value || Interlocked.CompareExchange(ref location, value, current) == current)
                {
                    return;
                }
            }
        }
    }
}
