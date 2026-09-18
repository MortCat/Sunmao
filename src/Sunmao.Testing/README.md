# Sunmao.Testing

Deterministic test helpers with no test framework dependency (works with xUnit, NUnit or MSTest).

## When to use

| Need | Use |
|---|---|
| Wait until asynchronous state reaches an expected value | `await TestWait.UntilAsync(() => …)` |
| Control time (backoff, timeouts, polling intervals) | inject `ManualTimeProvider` and move it with `Advance` |
| Test code with UI thread affinity (WPF, UI polling) | `SingleThreadUiTestHost.RunAsync(async () => …)` (Windows only) |
| Assert what was logged | `RecordingLogSink` |

## Minimal example

```csharp
var time = new ManualTimeProvider();
await using var poller = new MyPoller(time);      // code under test built with a TimeProvider
await poller.StartAsync();

// The code under test creates its timer on another thread: wait for it before advancing.
await TestWait.UntilAsync(() => time.ActiveTimerCount > 0);
time.Advance(TimeSpan.FromSeconds(1));

await TestWait.UntilAsync(() => poller.PollCount == 1, because: "one interval elapsed");
```

## Notes

- `ManualTimeProvider` runs timer callbacks synchronously on the thread that calls `Advance`; call
  `Advance` from one thread at a time.
- Inside `SingleThreadUiTestHost`, `.Wait()` and `.Result` deadlock exactly as on a real UI thread.

## Do not

- Use `Thread.Sleep` or "wait a fixed time, then check" in tests. Use `TestWait` or an explicit
  signal.
