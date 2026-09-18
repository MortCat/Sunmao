# Sunmao development rules

These are the standing rules of the Sunmao library, for people and AI agents alike. When an
implementation conflicts with them, state the conflict and update this file or `docs/decisions/`;
never work around a rule locally.

> Language: everything is written in English: code, identifiers, XML docs, this file, READMEs and
> `docs/`.

## 1. Scope

- Sunmao is a standalone shared library that belongs to no application. Add only what is
  domain-neutral and proven in real use, or what at least two projects need. Do not add
  abstractions because they might be useful.
- Keep the library free of application or domain vocabulary (product names, industry terms,
  customer names) in code, comments, samples and docs. Examples use neutral subjects such as
  devices, jobs and files.
- The library provides building blocks (lifecycle, polling, communication, WPF basics, theme, test
  helpers), not an application skeleton. The skeleton (composition root, page navigation shell,
  `PageViewModelBase`) lives only in `templates/sunmao-app`.

## 2. Packages and dependency direction

| Package | May reference | Third-party dependencies |
|---|---|---|
| `Sunmao.Core` | nothing | none |
| `Sunmao.Diagnostics`, `Sunmao.Testing`, `Sunmao.Wpf`, `Sunmao.Communication` | `Core` | none |
| `Sunmao.Wpf.Theme` | `Core`, `Wpf` | none |
| `Sunmao.Communication.Serial` | `Core`, `Communication` | `System.IO.Ports` |
| `Sunmao.Communication.Modbus` | `Core`, `Communication` | `NModbus` |

- `tools/verify_architecture.py` (A2) checks the direction. A new package or dependency updates
  that tool's `ALLOWED_DEPENDENCIES`, this table and the package README, with the reason.
- WPF appears only in `Sunmao.Wpf`, `Sunmao.Wpf.Theme` and `-windows` tests, samples and templates.
- Target frameworks: `net8.0;net10.0` for general packages, `net8.0-windows;net10.0-windows` for WPF
  packages. Do not use newer-runtime APIs without conditional compilation.
- Time always comes from `TimeProvider`; do not add clock interfaces. Tests use
  `Sunmao.Testing.ManualTimeProvider`.

## 3. Components and lifecycle

- A long-lived system boundary with a full lifecycle derives from `AppComponentBase`. The lifecycle
  is always `InitializeAsync` → `StartAsync` → `StopAsync` → `DisposeAsync`. Constructors do no I/O,
  open no connections and subscribe to no live callbacks.
- Business operations that must exclude lifecycle transitions use `RunExclusiveAsync`. Hooks and
  `StateChanged` handlers run while the gate is held: they must not call lifecycle methods or
  `RunExclusiveAsync`.
- `ComponentLifecycleCoordinator` initializes and starts Components in registration order and stops
  them in reverse.
- No `Manager` suffixes, and no shared `IService` that only carries lifecycle methods.

## 4. Periodic work and `PollingTaskBase`

- Every job that repeats on a fixed or adjustable interval is owned by a `PollingTaskBase`. Do not
  hand-roll `while` + `Task.Delay`, timers, `PeriodicTimer` or unowned tasks.
- A poller is always a private member of its owner (has-a). It is never a public class and never in
  a public inheritance chain (A5). The owner starts and stops it from its own lifecycle hooks.
- To wait one interval before the first poll, pass `delayFirstPoll` to the constructor. Do not delay
  inside `PollAsync`: the delay counts toward that iteration and the next one runs immediately.
- An interval changed by `PollingResult.ContinueAfter`, `PollingErrorDecision.RetryAfterDelay` or
  `SetInterval` stays in effect until changed again.
- The `OnStoppingAsync` token is cancelled when the owner calls `StopAsync`/`DisposeAsync`, and not
  cancelled when a poll returns `Stop` or the error policy stops the loop.
- `PollAsync` does bounded work per iteration and passes on and observes its `CancellationToken`.
  To end the loop it returns `PollingResult.Stop`; it never calls its own `StopAsync`.
- A non-periodic event or queue worker needs a single owner, bounded capacity, and cancellation and
  drain rules.

## 5. Concurrency

- Prefer publishing immutable snapshots; readers take the whole state with `Volatile`/`Interlocked`.
- `lock` is a last resort for very short in-memory invariants. No `await`, I/O or external callbacks
  inside a lock.
- Forbidden (A4): `new Thread`, `Thread.Sleep`, `Task.Factory.StartNew`, `Channel.CreateUnbounded`,
  fire-and-forget `_ = Task.Run(...)`, `PeriodicTimer`, `.GetAwaiter().GetResult()`.
- Before adding a `Task.Run`, timer, channel, `SemaphoreSlim`, lock or `CancellationTokenSource`,
  define its owner, capacity, stop order and failure policy. `tools/architecture_concurrency_baseline.json`
  (A6) is a ratchet of reviewed counts; every entry states its owner. Never edit it just to silence
  the check.
- Samples and templates (`samples/`, `templates/`) follow the same rules: people copy them.

## 6. Public API and versioning

- SemVer. New API bumps the minor version; any breaking change (removal, signature or semantic
  change) bumps the major version and documents the migration in `CHANGELOG.md`.
- Every public type and member has XML docs (enforced as a build error). Document the contract:
  threading, cancellation, exceptions, lifecycle requirements.
- Internal roles (pollers, executors, helpers) are `internal` by default; only types meant for users
  to compose are public.

## 7. Tests

- Every package has a `tests/<Package>.Tests` project. New behaviour gets deterministic tests.
- Never wait for results with `Thread.Sleep` or fixed delays in tests. Use `TestWait.UntilAsync`,
  `ManualTimeProvider` or explicit signals (`TaskCompletionSource`).
- Tests that need a UI thread use `SingleThreadUiTestHost`.
- Tests that need physical hardware are marked `Skip` with the hardware they need; CI never depends
  on devices.

## 8. Definition of done

Run from the repository root; everything must pass:

```
dotnet build Sunmao.slnx
dotnet test Sunmao.slnx --no-build
python tools/verify_architecture.py --self-test
python tools/verify_architecture.py --root .
git diff --check
```

When adding or changing a package, update its README (when to use it, a minimal example, what not
to do) and the package map in the root README.
