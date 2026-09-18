# Sunmao.Core

Dependency-free building blocks: component lifecycle, periodic polling, retry backoff and a logging
interface.

## When to use

| Need | Use |
|---|---|
| A long-lived system boundary that initializes, starts and stops (a device, a workflow…) | derive from `AppComponentBase` |
| Several Components started in order and stopped in reverse | `ComponentLifecycleCoordinator` |
| Work that repeats on a fixed or adjustable interval | a `private sealed class … : PollingTaskBase` inside its owner |
| Reconnects or retries with growing delays | `ExponentialBackoff` |
| Repeated failures without flooding the log | `ThrottledFailureLog` |
| Diagnostic messages | depend on `ILogSink` (the file implementation is in `Sunmao.Diagnostics`) |

## Minimal example

```csharp
public sealed class SensorComponent : AppComponentBase
{
    private readonly SensorPoller _poller;

    public SensorComponent(ILogSink log) : base(isEnabled: true, log) =>
        _poller = new SensorPoller(this, log);

    public int LastValue => Volatile.Read(ref _lastValue);
    private int _lastValue;

    protected override Task OnStartAsync(CancellationToken ct) => _poller.StartAsync(ct);
    protected override Task OnStopAsync(CancellationToken ct) => _poller.StopAsync();
    protected override ValueTask OnDisposeAsync() => _poller.DisposeAsync();

    // Private poller: not public and not in a public inheritance chain.
    private sealed class SensorPoller(SensorComponent owner, ILogSink log)
        : PollingTaskBase("sensor", log, TimeSpan.FromMilliseconds(100))
    {
        protected override ValueTask<PollingResult> PollAsync(CancellationToken ct)
        {
            Volatile.Write(ref owner._lastValue, Random.Shared.Next());
            return ValueTask.FromResult(PollingResult.Continue);
        }
    }
}
```

## Contract

- `PollAsync` does bounded work per iteration and observes its token. To end the loop it returns
  `PollingResult.Stop`; it never calls its own `StopAsync` from inside the loop.
- To wait one interval after start, pass `delayFirstPoll: true` to the constructor instead of
  delaying inside `PollAsync`.
- A changed interval (`ContinueAfter`, `RetryAfterDelay`, `SetInterval`) stays in effect.
- The `OnStoppingAsync` token is cancelled when the owner stops the poller, and not cancelled when
  the loop stops itself.
- `AppComponentBase` hooks and `StateChanged` handlers run while the lifecycle gate is held; they
  must not call lifecycle methods or `RunExclusiveAsync`.
- Time always comes from `TimeProvider`; tests use `Sunmao.Testing.ManualTimeProvider`.

## Do not

- Make a poller a public class or the base class of a Component.
- Hand-roll periodic work with `while` + `Task.Delay`, timers or `PeriodicTimer`.
- Share `ExponentialBackoff` or `ThrottledFailureLog` across threads: they belong to one owner loop.
