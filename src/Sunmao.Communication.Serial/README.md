# Sunmao.Communication.Serial

Serial port transport: `SerialTransport` implements `Sunmao.Communication.IByteTransport`, so it
works with `ManagedConnection` and `RequestResponseChannel`. It depends on `System.IO.Ports`, which is
why it is a separate package.

## Minimal example

```csharp
var transport = new SerialTransport(new SerialPortSettings("COM3", BaudRate: 115200));
await using var connection = new ManagedConnection(transport, log);
await connection.StartAsync();

using var channel = new RequestResponseChannel(connection.Transport, DelimiterFrameDecoder.CrLf());
byte[] reply = await channel.SendAsync("*IDN?\r\n"u8.ToArray(), TimeSpan.FromSeconds(1));
```

## Notes

- Serial reads cannot be cancelled in place on every platform, so **cancelling a receive closes the
  port**; `ManagedConnection` reopens it. `RequestResponseChannel` disconnects on timeout anyway, so
  the two agree.
- Open failures (missing or busy port) are reported as `IOException`.

## Do not

- Run two receives or two sends on one port at the same time; send requests through
  `RequestResponseChannel`.
