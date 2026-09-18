# Sunmao.Diagnostics

A non-blocking daily text log: `AsyncTextLogSink` implements `Sunmao.Core.Logging.ILogSink`.

## When to use

- The application writes diagnostics to files, and callers (the UI thread, pollers, device
  callbacks) must never wait on disk I/O.
- Logging starts early, but the data folder is only known after settings load: create the sink,
  write, and call `TryConfigure` later.

## Minimal example

```csharp
await using var log = new AsyncTextLogSink();
log.Write(LogLevel.Info, "startup", "Reading settings.");            // queued until a destination is set

log.TryConfigure(new LogFileDestination(@"D:\Data\logs", "myapp")); // writes myapp-2026-09-18.log
log.Write(LogLevel.Warning, "device", "Device 'pump-a' is not connected.");

var flush = await log.FlushAsync();   // when you must know it reached the disk, e.g. before exit
```

## Behaviour

- One file per local date: `{prefix}-yyyy-MM-dd.log`, UTF-8 without BOM, rolling over at midnight.
- The queue is bounded. Under pressure, Debug and Info entries are dropped first and
  `criticalReserve` slots stay free for Warning and Error. The number of dropped entries is logged
  as a summary line once writing recovers.
- `Snapshot` reports state and counters (queued, accepted, dropped per level, write failures) for
  display in a UI.
- `FlushAsync` reports success only when every earlier entry has been written.

## Do not

- Use it for records that must never be lost, such as audit or measurement results: diagnostics may
  be dropped under pressure.
- Create more than one sink per application. The composition root owns it and disposes it on exit.
