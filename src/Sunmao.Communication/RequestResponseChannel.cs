namespace Sunmao.Communication;

/// <summary>
/// Sends one request at a time over a transport and returns the next complete response frame.
/// </summary>
/// <remarks>
/// <para>
/// Requests from many callers are serialized, so each response belongs to the request that caused
/// it. Bytes left over from a previous exchange are discarded before each request.
/// </para>
/// <para>
/// An exchange that times out, is cancelled after the request was sent, or receives a malformed
/// frame is abandoned. The transport is then disconnected by default: a late or partial response
/// would otherwise be read as the answer to the next request. A <see cref="ManagedConnection"/>
/// reconnects it.
/// </para>
/// <code>
/// var channel = new RequestResponseChannel(connection.Transport, DelimiterFrameDecoder.CarriageReturn());
/// byte[] reply = await channel.SendAsync("STATUS?\r"u8.ToArray(), TimeSpan.FromSeconds(1), ct);
/// </code>
/// </remarks>
public sealed class RequestResponseChannel : IDisposable
{
    private readonly IByteTransport _transport;
    private readonly IFrameDecoder _decoder;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly byte[] _receiveBuffer;

    /// <summary>Creates a channel.</summary>
    /// <param name="transport">Transport to use; the channel does not own it.</param>
    /// <param name="decoder">Decoder that recognizes one response frame; owned by the channel.</param>
    /// <param name="defaultTimeout">Timeout when a call passes none; defaults to 1 second.</param>
    /// <param name="disconnectOnAbandon">Disconnect the transport when an exchange is abandoned; defaults to true.</param>
    /// <param name="receiveBufferSize">Size of the receive buffer.</param>
    /// <param name="timeProvider">Time source for timeouts; defaults to the system.</param>
    public RequestResponseChannel(
        IByteTransport transport,
        IFrameDecoder decoder,
        TimeSpan? defaultTimeout = null,
        bool disconnectOnAbandon = true,
        int receiveBufferSize = 4096,
        TimeProvider? timeProvider = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        DefaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(1);
        if (DefaultTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultTimeout));
        }

        DisconnectOnAbandon = disconnectOnAbandon;
        _receiveBuffer = new byte[receiveBufferSize > 0 ? receiveBufferSize : throw new ArgumentOutOfRangeException(nameof(receiveBufferSize))];
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Timeout used when a call passes none.</summary>
    public TimeSpan DefaultTimeout { get; }

    /// <summary>Whether an abandoned exchange (timeout, cancellation, malformed frame) disconnects the transport.</summary>
    public bool DisconnectOnAbandon { get; }

    /// <summary>Sends <paramref name="request"/> and waits for one response frame.</summary>
    /// <param name="request">Encoded request, including any framing the protocol needs.</param>
    /// <param name="timeout">Time allowed for sending and receiving; defaults to <see cref="DefaultTimeout"/>.</param>
    /// <param name="cancellationToken">Cancels waiting for the channel and the exchange.</param>
    /// <returns>The response frame as returned by the decoder.</returns>
    /// <exception cref="TimeoutException">No complete response arrived in time.</exception>
    /// <exception cref="IOException">The transport is not connected, failed, or was closed by the remote side.</exception>
    /// <exception cref="InvalidDataException">The response broke the decoder's limits.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<byte[]> SendAsync(
        ReadOnlyMemory<byte> request,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var limit = timeout ?? DefaultTimeout;
        if (limit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _decoder.Reset();
            using var timeoutSource = new CancellationTokenSource(limit, _timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            try
            {
                await _transport.SendAsync(request, linked.Token).ConfigureAwait(false);
                while (true)
                {
                    if (_decoder.TryReadFrame(out var frame))
                    {
                        return frame;
                    }

                    var received = await _transport.ReceiveAsync(_receiveBuffer, linked.Token).ConfigureAwait(false);
                    if (received == 0)
                    {
                        throw new IOException($"{_transport.Endpoint} closed the connection before responding.");
                    }

                    _decoder.Append(_receiveBuffer.AsSpan(0, received));
                }
            }
            catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                await AbandonAsync().ConfigureAwait(false);
                throw new TimeoutException($"{_transport.Endpoint} did not respond within {limit.TotalMilliseconds:0} ms.");
            }
            catch (Exception exception) when (exception is OperationCanceledException or InvalidDataException)
            {
                await AbandonAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Releases the channel's gate. The transport is not disposed.</summary>
    public void Dispose() => _gate.Dispose();

    private async Task AbandonAsync()
    {
        _decoder.Reset();
        if (DisconnectOnAbandon)
        {
            await _transport.DisconnectAsync().ConfigureAwait(false);
        }
    }
}
