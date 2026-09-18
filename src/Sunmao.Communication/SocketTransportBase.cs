using System.Net.Sockets;

namespace Sunmao.Communication;

/// <summary>Shared connect/disconnect gate and failure handling for socket transports.</summary>
public abstract class SocketTransportBase : IByteTransport
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Socket? _socket;
    private volatile bool _connected;
    private int _disposed;

    /// <inheritdoc />
    public abstract string Endpoint { get; }

    /// <inheritdoc />
    public bool IsConnected => _connected && Volatile.Read(ref _disposed) == 0;

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            if (_connected)
            {
                return;
            }

            CloseSocket();
            Socket socket;
            try
            {
                socket = await OpenSocketAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SocketException exception)
            {
                throw new IOException($"Connecting to {Endpoint} failed: {exception.Message}", exception);
            }

            _socket = socket;
            _connected = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            CloseSocket();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var socket = CurrentSocket();
        try
        {
            while (!data.IsEmpty)
            {
                var sent = await socket.SendAsync(data, SocketFlags.None, cancellationToken).ConfigureAwait(false);
                data = data[sent..];
            }
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            _connected = false;
            throw new IOException($"Sending to {Endpoint} failed.", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var socket = CurrentSocket();
        try
        {
            var received = await socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
            if (received == 0 && IsStream)
            {
                _connected = false;
            }

            return received;
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            _connected = false;
            throw new IOException($"Receiving from {Endpoint} failed.", exception);
        }
    }

    /// <summary>Closes the socket and prevents further use.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await DisconnectAsync().ConfigureAwait(false);
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>True for stream sockets, where receiving 0 bytes means the remote side closed.</summary>
    protected abstract bool IsStream { get; }

    /// <summary>Creates and connects a new socket; dispose it on failure.</summary>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    protected abstract Task<Socket> OpenSocketAsync(CancellationToken cancellationToken);

    private Socket CurrentSocket()
    {
        var socket = Volatile.Read(ref _socket);
        if (!_connected || socket is null)
        {
            throw new IOException($"{Endpoint} is not connected.");
        }

        return socket;
    }

    private void CloseSocket()
    {
        _connected = false;
        var socket = Interlocked.Exchange(ref _socket, null);
        if (socket is null)
        {
            return;
        }

        try
        {
            if (IsStream && socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
            }
        }
        catch (SocketException)
        {
            // The peer may already be gone; closing is still correct.
        }
        finally
        {
            socket.Dispose();
        }
    }
}

/// <summary>TCP client transport.</summary>
/// <param name="host">Host name or IP address.</param>
/// <param name="port">Remote port.</param>
/// <param name="noDelay">Disables Nagle's algorithm; true suits small request/response messages.</param>
public sealed class TcpTransport(string host, int port, bool noDelay = true) : SocketTransportBase
{
    private readonly string _host = string.IsNullOrWhiteSpace(host) ? throw new ArgumentException("A host is required.", nameof(host)) : host;
    private readonly int _port = port is > 0 and <= 65535 ? port : throw new ArgumentOutOfRangeException(nameof(port));

    /// <inheritdoc />
    public override string Endpoint => $"tcp://{_host}:{_port}";

    /// <inheritdoc />
    protected override bool IsStream => true;

    /// <inheritdoc />
    protected override async Task<Socket> OpenSocketAsync(CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = noDelay };
        try
        {
            await socket.ConnectAsync(_host, _port, cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}

/// <summary>
/// UDP transport bound to one remote peer: sends go to it and only its datagrams are received.
/// Each receive returns one datagram; pass a buffer large enough for the largest datagram.
/// </summary>
/// <param name="host">Remote host name or IP address.</param>
/// <param name="port">Remote port.</param>
/// <param name="localPort">Local port to bind, or 0 for any free port.</param>
public sealed class UdpTransport(string host, int port, int localPort = 0) : SocketTransportBase
{
    private readonly string _host = string.IsNullOrWhiteSpace(host) ? throw new ArgumentException("A host is required.", nameof(host)) : host;
    private readonly int _port = port is > 0 and <= 65535 ? port : throw new ArgumentOutOfRangeException(nameof(port));
    private readonly int _localPort = localPort is >= 0 and <= 65535 ? localPort : throw new ArgumentOutOfRangeException(nameof(localPort));

    /// <inheritdoc />
    public override string Endpoint => $"udp://{_host}:{_port}";

    /// <inheritdoc />
    protected override bool IsStream => false;

    /// <inheritdoc />
    protected override async Task<Socket> OpenSocketAsync(CancellationToken cancellationToken)
    {
        var addresses = await System.Net.Dns.GetHostAddressesAsync(_host, cancellationToken).ConfigureAwait(false);
        var address = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork) ?? addresses[0];
        var socket = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            var any = address.AddressFamily == AddressFamily.InterNetworkV6 ? System.Net.IPAddress.IPv6Any : System.Net.IPAddress.Any;
            socket.Bind(new System.Net.IPEndPoint(any, _localPort));
            await socket.ConnectAsync(new System.Net.IPEndPoint(address, _port), cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
