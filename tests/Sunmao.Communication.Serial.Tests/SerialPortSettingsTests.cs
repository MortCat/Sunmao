using System.IO.Ports;
using Sunmao.Communication.Serial;

namespace Sunmao.Communication.Serial.Tests;

public sealed class SerialPortSettingsTests
{
    [Fact]
    public async Task ValidSettingsProduceAStableEndpoint()
    {
        var settings = new SerialPortSettings(
            "COM7",
            BaudRate: 115200,
            Parity: Parity.Even,
            DataBits: 7,
            StopBits: StopBits.Two,
            Handshake: Handshake.RequestToSend);

        settings.Validate();

        await using var transport = new SerialTransport(settings);

        Assert.Equal("serial://COM7?baud=115200", transport.Endpoint);
        Assert.False(transport.IsConnected);
    }

    [Fact]
    public void InvalidSettingsAreRejectedBeforeOpeningHardware()
    {
        Assert.Throws<ArgumentException>(() => new SerialPortSettings("").Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialPortSettings("COM1", BaudRate: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialPortSettings("COM1", DataBits: 4).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialPortSettings("COM1", StopBits: StopBits.None).Validate());
    }

    [Fact(Skip = "Platform-specific port enumeration check; excluded from CI because port names vary.")]
    public async Task UnavailablePortReportsAnIoFailure()
    {
        await using var transport = new SerialTransport(new SerialPortSettings("SUNMAO_MISSING_PORT"));

        await Assert.ThrowsAsync<IOException>(() => transport.ConnectAsync(CancellationToken.None));

        Assert.False(transport.IsConnected);
    }
}
