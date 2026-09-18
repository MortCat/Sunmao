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
| `Sunmao.Diagnostics` | Asynchronous text log (bounded queue, one file per day) | net8.0, net10.0 |
| `Sunmao.Testing` | Deterministic test helpers: `TestWait`, `ManualTimeProvider`, single-thread UI test host | net8.0, net10.0 |
| `Sunmao.Wpf` | MVVM basics, asynchronous commands, shared UI polling | net8.0-windows, net10.0-windows |
| `Sunmao.Wpf.Theme` | Design tokens, Light/Dark themes, Touch density, control styles and controls | net8.0-windows, net10.0-windows |
| `Sunmao.Communication` | TCP/UDP transports, framing, request/response, self-healing connections | net8.0, net10.0 |
| `Sunmao.Communication.Serial` | Serial port transport | net8.0, net10.0 |
| `Sunmao.Communication.Modbus` | Modbus TCP client, register polling, rising-edge triggers (wraps NModbus) | net8.0, net10.0 |

Each package folder has a README covering when to use it, a minimal example and what not to do.
Development rules are in [AGENTS.md](AGENTS.md).

## Starting a new project

`templates/sunmao-app` is a `dotnet new` template: a composition root, one Component with a private
poller, one Service, page navigation and theme switching, plus a test project and the architecture
checks.

## Build and verify

```
dotnet build Sunmao.slnx
dotnet test Sunmao.slnx --no-build
python tools/verify_architecture.py --self-test
python tools/verify_architecture.py --root .
```

## License

Sunmao is released under the [MIT License](LICENSE).
