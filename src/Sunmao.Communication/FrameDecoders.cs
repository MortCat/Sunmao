using System.Buffers.Binary;

namespace Sunmao.Communication;

/// <summary>
/// Splits a byte stream into frames. Feed received bytes with <see cref="Append"/> and take complete
/// frames with <see cref="TryReadFrame"/>. Not thread-safe: one reader owns a decoder.
/// </summary>
public interface IFrameDecoder
{
    /// <summary>Bytes received but not yet returned as a frame.</summary>
    int BufferedBytes { get; }

    /// <summary>Adds received bytes. Call <see cref="TryReadFrame"/> until it returns false after each append.</summary>
    /// <param name="data">Bytes in arrival order.</param>
    void Append(ReadOnlySpan<byte> data);

    /// <summary>Returns the next complete frame, if any.</summary>
    /// <param name="frame">The frame payload.</param>
    /// <exception cref="InvalidDataException">A frame exceeds the configured maximum; the buffer is cleared.</exception>
    bool TryReadFrame(out byte[] frame);

    /// <summary>Discards buffered bytes, for example after a timeout or reconnect.</summary>
    void Reset();
}

/// <summary>Growable byte buffer shared by the decoders.</summary>
public abstract class FrameDecoderBase : IFrameDecoder
{
    private byte[] _buffer = new byte[256];
    private int _start;
    private int _length;

    /// <summary>Creates a decoder with a maximum frame size.</summary>
    /// <param name="maxFrameLength">Largest accepted frame; larger input is a protocol error.</param>
    protected FrameDecoderBase(int maxFrameLength)
    {
        MaxFrameLength = maxFrameLength > 0 ? maxFrameLength : throw new ArgumentOutOfRangeException(nameof(maxFrameLength));
    }

    /// <summary>Largest accepted frame, in bytes.</summary>
    public int MaxFrameLength { get; }

    /// <inheritdoc />
    public int BufferedBytes => _length;

    /// <summary>The buffered, not yet consumed bytes.</summary>
    protected ReadOnlySpan<byte> Buffered => _buffer.AsSpan(_start, _length);

    /// <inheritdoc />
    public void Append(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return;
        }

        EnsureCapacity(_length + data.Length);
        data.CopyTo(_buffer.AsSpan(_start + _length));
        _length += data.Length;
    }

    /// <inheritdoc />
    public abstract bool TryReadFrame(out byte[] frame);

    /// <inheritdoc />
    public void Reset()
    {
        _start = 0;
        _length = 0;
    }

    /// <summary>Clears the buffer and throws, for a frame that breaks the protocol limits.</summary>
    /// <param name="message">What was wrong.</param>
    protected InvalidDataException Oversized(string message)
    {
        Reset();
        return new InvalidDataException(message);
    }

    /// <summary>Removes <paramref name="count"/> bytes from the front of the buffer.</summary>
    /// <param name="count">Bytes consumed.</param>
    protected void Consume(int count)
    {
        _start += count;
        _length -= count;
        if (_length == 0)
        {
            _start = 0;
        }
    }

    private void EnsureCapacity(int required)
    {
        if (_start + required <= _buffer.Length)
        {
            return;
        }

        if (required <= _buffer.Length)
        {
            Buffer.BlockCopy(_buffer, _start, _buffer, 0, _length);
        }
        else
        {
            var larger = new byte[Math.Max(required, _buffer.Length * 2)];
            Buffer.BlockCopy(_buffer, _start, larger, 0, _length);
            _buffer = larger;
        }

        _start = 0;
    }
}

/// <summary>
/// Frames terminated by a delimiter, for ASCII command protocols (for example a trailing CR).
/// Returned frames exclude the delimiter.
/// </summary>
public sealed class DelimiterFrameDecoder : FrameDecoderBase
{
    private readonly byte[] _delimiter;

    /// <summary>Creates a decoder for <paramref name="delimiter"/>.</summary>
    /// <param name="delimiter">Non-empty terminator sequence.</param>
    /// <param name="maxFrameLength">Largest frame without the delimiter; defaults to 64 KiB.</param>
    public DelimiterFrameDecoder(ReadOnlySpan<byte> delimiter, int maxFrameLength = 64 * 1024)
        : base(maxFrameLength)
    {
        if (delimiter.IsEmpty)
        {
            throw new ArgumentException("A delimiter is required.", nameof(delimiter));
        }

        _delimiter = delimiter.ToArray();
    }

    /// <summary>Carriage return (<c>\r</c>) delimited frames.</summary>
    public static DelimiterFrameDecoder CarriageReturn(int maxFrameLength = 64 * 1024) => new("\r"u8, maxFrameLength);

    /// <summary>Line feed (<c>\n</c>) delimited frames.</summary>
    public static DelimiterFrameDecoder LineFeed(int maxFrameLength = 64 * 1024) => new("\n"u8, maxFrameLength);

    /// <summary>CR LF (<c>\r\n</c>) delimited frames.</summary>
    public static DelimiterFrameDecoder CrLf(int maxFrameLength = 64 * 1024) => new("\r\n"u8, maxFrameLength);

    /// <inheritdoc />
    public override bool TryReadFrame(out byte[] frame)
    {
        var index = Buffered.IndexOf(_delimiter);
        if (index < 0)
        {
            frame = [];
            if (BufferedBytes >= MaxFrameLength + _delimiter.Length)
            {
                throw Oversized($"No delimiter within {MaxFrameLength} bytes.");
            }

            return false;
        }

        if (index > MaxFrameLength)
        {
            throw Oversized($"Frame of {index} bytes exceeds {MaxFrameLength} bytes.");
        }

        frame = Buffered[..index].ToArray();
        Consume(index + _delimiter.Length);
        return true;
    }
}

/// <summary>
/// Frames with a binary length prefix. Returned frames exclude the prefix; <see cref="Encode"/> adds it.
/// </summary>
public sealed class LengthPrefixFrameDecoder : FrameDecoderBase
{
    /// <summary>Creates a decoder.</summary>
    /// <param name="prefixLength">Prefix size in bytes: 1, 2 or 4.</param>
    /// <param name="bigEndian">True for network byte order.</param>
    /// <param name="lengthIncludesPrefix">True when the length counts the prefix bytes too.</param>
    /// <param name="maxFrameLength">Largest payload; defaults to 64 KiB.</param>
    public LengthPrefixFrameDecoder(int prefixLength = 2, bool bigEndian = true, bool lengthIncludesPrefix = false, int maxFrameLength = 64 * 1024)
        : base(maxFrameLength)
    {
        PrefixLength = prefixLength is 1 or 2 or 4 ? prefixLength : throw new ArgumentOutOfRangeException(nameof(prefixLength));
        BigEndian = bigEndian;
        LengthIncludesPrefix = lengthIncludesPrefix;
    }

    /// <summary>Prefix size in bytes.</summary>
    public int PrefixLength { get; }

    /// <summary>True for network byte order.</summary>
    public bool BigEndian { get; }

    /// <summary>True when the length counts the prefix bytes too.</summary>
    public bool LengthIncludesPrefix { get; }

    /// <summary>Returns <paramref name="payload"/> with this decoder's prefix in front.</summary>
    /// <param name="payload">Frame payload.</param>
    public byte[] Encode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxFrameLength)
        {
            throw new ArgumentException($"Payload exceeds {MaxFrameLength} bytes.", nameof(payload));
        }

        var length = payload.Length + (LengthIncludesPrefix ? PrefixLength : 0);
        var frame = new byte[PrefixLength + payload.Length];
        WritePrefix(frame.AsSpan(0, PrefixLength), length);
        payload.CopyTo(frame.AsSpan(PrefixLength));
        return frame;
    }

    /// <inheritdoc />
    public override bool TryReadFrame(out byte[] frame)
    {
        frame = [];
        if (BufferedBytes < PrefixLength)
        {
            return false;
        }

        var declared = ReadPrefix(Buffered[..PrefixLength]);
        var payloadLength = declared - (LengthIncludesPrefix ? PrefixLength : 0);
        if (payloadLength < 0 || payloadLength > MaxFrameLength)
        {
            throw Oversized($"Declared frame length {declared} is outside 0..{MaxFrameLength}.");
        }

        if (BufferedBytes < PrefixLength + payloadLength)
        {
            return false;
        }

        frame = Buffered.Slice(PrefixLength, (int)payloadLength).ToArray();
        Consume(PrefixLength + (int)payloadLength);
        return true;
    }

    private long ReadPrefix(ReadOnlySpan<byte> prefix) => PrefixLength switch
    {
        1 => prefix[0],
        2 => BigEndian ? BinaryPrimitives.ReadUInt16BigEndian(prefix) : BinaryPrimitives.ReadUInt16LittleEndian(prefix),
        _ => BigEndian ? BinaryPrimitives.ReadUInt32BigEndian(prefix) : BinaryPrimitives.ReadUInt32LittleEndian(prefix)
    };

    private void WritePrefix(Span<byte> prefix, int length)
    {
        switch (PrefixLength)
        {
            case 1:
                prefix[0] = checked((byte)length);
                break;
            case 2:
                if (BigEndian)
                {
                    BinaryPrimitives.WriteUInt16BigEndian(prefix, checked((ushort)length));
                }
                else
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(prefix, checked((ushort)length));
                }

                break;
            default:
                if (BigEndian)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(prefix, (uint)length);
                }
                else
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(prefix, (uint)length);
                }

                break;
        }
    }
}
