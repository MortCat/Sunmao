using System.Net;
using System.Net.Sockets;
using System.Text;
using Sunmao.Testing;

namespace Sunmao.Communication.Tests;

/// <summary>
/// Smoke tests only; the full scenario suite (backoff timing, abandon rules, UDP, serial) is deferred.
/// </summary>
public sealed class CommunicationSmokeTests
{
    [Fact]
    public void DelimiterDecoderJoinsSplitChunksAndSplitsCoalescedFrames()
    {
        var decoder = DelimiterFrameDecoder.CrLf(maxFrameLength: 4);

        decoder.Append("AB\r"u8);
        Assert.False(decoder.TryReadFrame(out _));
        decoder.Append("\nCD\r\nEF\r\n"u8);

        var frames = new List<string>();
        while (decoder.TryReadFrame(out var frame))
        {
            frames.Add(Encoding.ASCII.GetString(frame));
        }

        Assert.Equal(["AB", "CD", "EF"], frames);
        Assert.Equal(0, decoder.BufferedBytes);
    }

    [Fact]
    public async Task RequestResponseChannelRoundTripsOverTcp()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var server = EchoOneClientAsync(listener, requests: 2);

        await using var transport = new TcpTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);
        await transport.ConnectAsync(CancellationToken.None);
        using var channel = new RequestResponseChannel(transport, DelimiterFrameDecoder.CarriageReturn(), TimeSpan.FromSeconds(5));

        var first = await channel.SendAsync("A\r"u8.ToArray());
        var second = await channel.SendAsync("B\r"u8.ToArray());

        Assert.Equal("ACK:A", Encoding.ASCII.GetString(first));
        Assert.Equal("ACK:B", Encoding.ASCII.GetString(second));
        await server.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ManagedConnectionReconnectsAfterTheServerDropsIt()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        await using var connection = new ManagedConnection(
            new TcpTransport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port),
            checkInterval: TimeSpan.FromMilliseconds(20),
            retryInitialDelay: TimeSpan.FromMilliseconds(20));
        using var channel = new RequestResponseChannel(connection.Transport, DelimiterFrameDecoder.CarriageReturn(), TimeSpan.FromSeconds(5));

        await connection.StartAsync();
        using (var dropped = await listener.AcceptSocketAsync())
        {
            await TestWait.UntilAsync(() => connection.Snapshot.IsConnected, because: "the first connect should succeed");
        }

        // The drop is only visible through I/O: the request fails and marks the transport disconnected.
        await Assert.ThrowsAsync<IOException>(() => channel.SendAsync("A\r"u8.ToArray()));
        var server = EchoOneClientAsync(listener, requests: 1);
        // Snapshot still reads Connected until the next check; the transport itself reports the reconnect.
        await TestWait.UntilAsync(() => connection.Transport.IsConnected, because: "the supervisor should reconnect");

        var reply = await channel.SendAsync("B\r"u8.ToArray());

        Assert.Equal("ACK:B", Encoding.ASCII.GetString(reply));
        await server.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static async Task EchoOneClientAsync(TcpListener listener, int requests)
    {
        using var client = await listener.AcceptSocketAsync();
        var decoder = DelimiterFrameDecoder.CarriageReturn();
        var buffer = new byte[256];
        var answered = 0;
        while (answered < requests)
        {
            var received = await client.ReceiveAsync(buffer, SocketFlags.None);
            if (received == 0)
            {
                return;
            }

            decoder.Append(buffer.AsSpan(0, received));
            while (answered < requests && decoder.TryReadFrame(out var frame))
            {
                await client.SendAsync(Encoding.ASCII.GetBytes($"ACK:{Encoding.ASCII.GetString(frame)}\r"), SocketFlags.None);
                answered++;
            }
        }
    }
}
