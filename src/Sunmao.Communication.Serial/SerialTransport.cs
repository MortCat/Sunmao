using System.IO.Ports;

namespace Sunmao.Communication.Serial;

/// <summary>Serial line settings.</summary>
/// <param name="PortName">Port name, for example <c>COM3</c>.</param>
/// <param name="BaudRate">Bits per second.</param>
/// <param name="Parity">Parity checking.</param>
/// <param name="DataBits">Data bits per byte, 5 to 8.</param>
/// <param name="StopBits">Stop bits.</param>
/// <param name="Handshake">Flow control.</param>
public sealed record SerialPortSettings(
    string PortName,
    int BaudRate = 9600,
    Parity Parity = Parity.None,
    int DataBits = 8,
    StopBits StopBits = StopBits.One,
    Handshake Handshake = Handshake.None)
{
    /// <summary>Throws when a setting is out of range.</summary>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PortName);
        if (BaudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BaudRate));
        }

        if (DataBits is < 5 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(DataBits));
        }

        if (StopBits == StopBits.None)
        {
            throw new ArgumentOutOfRangeException(nameof(StopBits), "Serial ports need at least one stop bit.");
        }
    }
}

/// <summary>
/// <see cref="IByteTransport"/> over a serial port.
/// </summary>
/// <remarks>
/// Serial reads cannot be cancelled in place on every platform, so cancelling a receive closes the
/// port: <see cref="IByteTransport.IsConnected"/> becomes false and a <see cref="ManagedConnection"/>
/// reopens it. This matches <see cref="RequestResponseChannel"/>, which disconnects on timeout anyway.
/// </remarks>
public sealed class SerialTransport : IByteTransport
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SerialPortSettings _settings;
    private SerialPort? _port;
    private volatile bool _connected;
    private int _disposed;

    /// <summary>Creates a closed transport.</summary>
    /// <param name="settings">Line settings; validated here.</param>
    public SerialTransport(SerialPortSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _settings = settings;
    }

    /// <inheritdoc />
    public string Endpoint => $"serial://{_settings.PortName}?baud={_settings.BaudRate}";

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

            ClosePort();
            var port = new SerialPort(_settings.PortName, _settings.BaudRate, _settings.Parity, _settings.DataBits, _settings.StopBits)
            {
                Handshake = _settings.Handshake
            };
            try
            {
                port.Open();
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                port.Dispose();
                throw new IOException($"Opening {Endpoint} failed.", exception);
            }
            catch
            {
                port.Dispose();
                throw;
            }

            _port = port;
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
            ClosePort();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var stream = CurrentStream();
        try
        {
            await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ObjectDisposedException)
        {
            _connected = false;
            throw new IOException($"Writing to {Endpoint} failed.", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var stream = CurrentStream();
        try
        {
            return await stream.ReadAsync(buffer, CancellationToken.None).AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The pending read cannot be abandoned safely; closing the port completes it.
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ObjectDisposedException)
        {
            _connected = false;
            throw new IOException($"Reading from {Endpoint} failed.", exception);
        }
    }

    /// <summary>Closes the port and prevents further use.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await DisconnectAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private Stream CurrentStream()
    {
        var port = Volatile.Read(ref _port);
        if (!_connected || port is null)
        {
            throw new IOException($"{Endpoint} is not connected.");
        }

        return port.BaseStream;
    }

    private void ClosePort()
    {
        _connected = false;
        var port = Interlocked.Exchange(ref _port, null);
        port?.Dispose();
    }
}
