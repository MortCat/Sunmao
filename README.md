# Sunmao

Sunmao (榫卯, mortise-and-tenon joinery) is a shared .NET library for building applications on one
consistent architecture. The name comes from woodworking joints that hold a structure together
without nails: modules connect through standard interfaces, stay solid when assembled, and can be
replaced without damaging the rest.

New projects and AI agents start from the same proven building blocks: component lifecycle,
periodic polling, communication, WPF basics, a theme and test helpers.

## Packages

| Package | Purpose | Target frameworks |
|---|---|---|
| `Sunmao.Core` | Component lifecycle, `PollingTaskBase`, backoff and failure throttling, logging interface | net8.0, net10.0 |
| `Sunmao.Numerics` | Double-precision 3D vectors, normalized rotations and rigid coordinate transforms; no dependencies | net8.0, net10.0 |
| `Sunmao.Diagnostics` | Asynchronous text log (bounded queue, one file per day) | net8.0, net10.0 |
| `Sunmao.Testing` | Deterministic test helpers: `TestWait`, `ManualTimeProvider`, single-thread UI test host | net8.0, net10.0 |
| `Sunmao.Wpf` | MVVM basics, asynchronous commands, shared UI polling | net8.0-windows, net10.0-windows |
| `Sunmao.Wpf.Theme` | Design tokens, Light/Dark themes, Touch density, control styles and controls | net8.0-windows, net10.0-windows |
| `Sunmao.Communication` | TCP/UDP transports, framing, request/response, self-healing connections | net8.0, net10.0 |
| `Sunmao.Communication.Serial` | Serial port transport | net8.0, net10.0 |

Each package folder has a README covering when to use it, a minimal example and what not to do.
Development rules are in [AGENTS.md](AGENTS.md).

The [development plan](docs/development-plan.md) records proposed expansion stages, current gaps
and acceptance gates. Planned capabilities are not available APIs.
The [mathematical foundations plan](docs/mathematical-foundations-plan.md) records the computation
expansion priority and the bounded first numerical package.

## Starting a new project

`templates/sunmao-app` is a `dotnet new` application template. It contains a composition root, one
Component with a private poller, a snapshot-driven page, theme switching and a test project. The
generated project uses versioned package references; verify it with a local or remote Sunmao package
feed.

## Build and verify

Local agents can start with the [capability catalog and retrieval guide](agent/README.md) and
[executable recipes](recipes/README.md). Each entry links to source, contracts and tests.
Run the fixed verification profiles to obtain JSON results and per-step logs:

```powershell
powershell -NoProfile -File tools/verify.ps1 -Profile catalog
powershell -NoProfile -File tools/verify.ps1 -Profile recipes
powershell -NoProfile -File tools/verify.ps1 -Profile full
```

The full profile includes the commands below and generated application verification.

```
dotnet build Sunmao.slnx
dotnet test Sunmao.slnx --no-build
python tools/verify_architecture.py --self-test
python tools/verify_architecture.py --root .
```

## License

Sunmao is released under the [MIT License](LICENSE).
