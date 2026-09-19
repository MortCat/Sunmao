using System.Buffers.Binary;
using System.Text;

namespace Sunmao.Communication.Tests;

public sealed class FrameDecoderTests
{
    [Fact]
    public void LengthPrefixDecoderHandlesSplitFramesAndCoalescedFrames()
    {
        var decoder = new LengthPrefixFrameDecoder(prefixLength: 2, bigEndian: true);
        var first = decoder.Encode("one"u8);
        var second = decoder.Encode("two"u8);

        decoder.Append(first.AsSpan(0, 1));
        Assert.False(decoder.TryReadFrame(out _));

        decoder.Append(first.AsSpan(1));
        decoder.Append(second);

        Assert.Equal("one", Encoding.ASCII.GetString(ReadFrame(decoder)));
        Assert.Equal("two", Encoding.ASCII.GetString(ReadFrame(decoder)));
        Assert.Equal(0, decoder.BufferedBytes);
    }

    [Fact]
    public void LengthPrefixDecoderSupportsLengthsThatIncludeThePrefix()
    {
        var decoder = new LengthPrefixFrameDecoder(prefixLength: 2, bigEndian: false, lengthIncludesPrefix: true);

        var encoded = decoder.Encode("abc"u8);

        Assert.Equal(5, BinaryPrimitives.ReadUInt16LittleEndian(encoded));
        decoder.Append(encoded);

        Assert.Equal("abc", Encoding.ASCII.GetString(ReadFrame(decoder)));
    }

    [Fact]
    public void InvalidLengthClearsTheDecoder()
    {
        var decoder = new LengthPrefixFrameDecoder(prefixLength: 2, maxFrameLength: 3);

        decoder.Append([0, 4]);

        Assert.Throws<InvalidDataException>(() => decoder.TryReadFrame(out _));
        Assert.Equal(0, decoder.BufferedBytes);
    }

    [Fact]
    public void DelimiterDecoderRejectsAFrameThatExceedsItsLimit()
    {
        var decoder = DelimiterFrameDecoder.CarriageReturn(maxFrameLength: 3);

        decoder.Append("ABCD"u8);

        Assert.Throws<InvalidDataException>(() => decoder.TryReadFrame(out _));
        Assert.Equal(0, decoder.BufferedBytes);
    }

    private static byte[] ReadFrame(IFrameDecoder decoder)
    {
        Assert.True(decoder.TryReadFrame(out var frame));
        return frame;
    }
}
