# Sunmao.Communication

Communication basics: byte transports, framing, a request/response channel and self-healing
connections. It depends only on `Sunmao.Core`; concrete protocols (Modbus, serial…) live in their own
packages on top of these types.

| Type | Purpose |
|---|---|
| `IByteTransport` | A bidirectional byte link: `ConnectAsync`, `DisconnectAsync`, `SendAsync`, `ReceiveAsync`; failures are `IOException` |
| `TcpTransport`, `UdpTransport` | Socket implementations (UDP is bound to one peer; each receive returns one datagram) |
| `DelimiterFrameDecoder`, `LengthPrefixFrameDecoder` | Split a byte stream into frames, with a maximum frame size |
| `RequestResponseChannel` | One request at a time with timeouts; abandoned exchanges disconnect |
| `ManagedConnection` | A private supervisor poller keeps a link connected: immediate retry after a drop, exponential backoff, throttled log, state snapshots and events |

## When to use

- A device speaks "send one command, wait for one reply": `ManagedConnection` +
  `RequestResponseChannel`.
- A UI shows the connection state: read `ManagedConnection.Snapshot` (immutable, safe from any
  thread) or subscribe to `StateChanged` (raised on the supervisor thread; keep handlers short).
- A new protocol: implement `IFrameDecoder` (or reuse one) and put a typed API on top of the channel.

## Minimal example

```csharp
await using var connection = new ManagedConnection(new TcpTransport("192.168.0.10", 5000), log);
await connection.StartAsync();

using var channel = new RequestResponseChannel(connection.Transport, DelimiterFrameDecoder.CarriageReturn());
byte[] reply = await channel.SendAsync("STATUS?\r"u8.ToArray(), TimeSpan.FromSeconds(1), ct);
```

## Behaviour

- **Reconnect**: the first connect and the first retry after a drop run immediately. Each failed
  attempt then waits 0.5 s, doubling up to 5 s. The first failure is logged at once, repeated
  failures are summarized every 60 s, and a recovery is logged once.
- **Drops are detected through I/O**: when a TCP peer vanishes silently, the next send or receive
  fails; the supervisor reconnects on its next check.
- **Abandoned exchanges**: a timeout, a cancellation after sending, or a malformed frame disconnects
  the transport by default (`disconnectOnAbandon`), so a late reply is never read as the answer to
  the next request.

## Do not

- Run two `ReceiveAsync` or two `SendAsync` calls on one transport at the same time. Request/response
  traffic always goes through `RequestResponseChannel`.
- Do I/O or long work in `StateChanged` handlers.
- Dispose the transport yourself: `ManagedConnection` owns and disposes it.
