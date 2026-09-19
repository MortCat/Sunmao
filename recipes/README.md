# Executable recipes

Each Example.cs is compiled directly into a solution test project. Run one recipe using the command
in its README, or all recipes with powershell -NoProfile -File tools/verify.ps1 -Profile recipes.
The examples are internal development assets and introduce no new runtime package or public API.

| Recipe | Purpose |
|---|---|
| [coordinate-transform](coordinate-transform/README.md) | Double-precision point mapping, composition and inverse |
| [lifecycle](lifecycle/README.md) | Ordered composition and cleanup after startup failure |
| [private-polling](private-polling/README.md) | Component-owned periodic work and drained stop |
| [reconnecting-communication](reconnecting-communication/README.md) | Failed connection, retry, recovery and transport ownership |
| [command-cancellation](command-cancellation/README.md) | UI command cancellation and awaited cleanup |
| [snapshot-ui](snapshot-ui/README.md) | Immutable state projection and subscription lifetime |

Recipes follow the same architecture checks as src, samples and templates. The tests use manual time
or explicit signals. UI recipes run through SingleThreadUiTestHost. No recipe requires physical hardware.
