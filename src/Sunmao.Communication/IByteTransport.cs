namespace Sunmao.Communication;

/// <summary>
/// A bidirectional byte link such as a TCP socket, a UDP peer or a serial port.
/// </summary>
/// <remarks>
/// <para>
/// Connect and disconnect are serialized by the transport. One sender and one receiver may run at
/// the same time, but never two concurrent sends or two concurrent receives; use
/// <see cref="RequestResponseChannel"/> to serialize request/response traffic.
/// </para>
/// <para>
/// I/O failures surface as <see cref="IOException"/> and leave <see cref="IsConnected"/> false, so a
/// <see cref="ManagedConnection"/> supervising the transport reconnects it.
/// </para>
/// </remarks>
public interface IByteTransport : IAsyncDisposable
{
    /// <summary>Human-readable address, for example <c>tcp://192.168.0.10:502</c>.</summary>
    string Endpoint { get; }

    /// <summary>True while the link is open and no I/O failure has been observed.</summary>
    bool IsConnected { get; }

    /// <summary>Opens the link. Does nothing when already connected.</summary>
    /// <param name="cancellationToken">Cancels the attempt; use it for connect timeouts.</param>
    /// <exception cref="IOException">The link could not be opened.</exception>
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Closes the link. Does nothing when already closed.</summary>
    Task DisconnectAsync();

    /// <summary>Sends all of <paramref name="data"/>.</summary>
    /// <param name="data">Bytes to send.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <exception cref="IOException">The link is not connected or failed.</exception>
    ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    /// <summary>
    /// Receives available bytes into <paramref name="buffer"/>, waiting for at least one byte.
    /// </summary>
    /// <param name="buffer">Destination buffer.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Number of bytes received; 0 when the remote side closed the link.</returns>
    /// <exception cref="IOException">The link is not connected or failed.</exception>
    ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);
}
