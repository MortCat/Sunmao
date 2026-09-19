# snapshot-ui

Project immutable backend snapshots on an owned UI pulse.

Lifecycle: create the source and projection; start the shared pulse and activate the projection.

Threading: exactly one backend writer publishes complete immutable snapshots with Volatile.

The projection and its subscription are UI-thread-only; the pulse callback performs no I/O.

Cancellation: no asynchronous operation is owned here; deactivate disables delivery synchronously.

Errors: callback failures are isolated and logged by the shared UI pulse; use a diagnostic sink.

Ownership: the composition root owns the pulse; the projection owns only its subscription.

Cleanup: dispose the projection subscription before disposing the shared pulse.

Intermediate values may be skipped. A single snapshot reference preserves field consistency.

The executable test drives ManualUiPulseDriver; real apps can supply UiPollingTimer.

Value is a public getter for WPF binding. The test binds a TextBlock to Value.Value and checks
the displayed text after projection and deactivation; it does not test rendered appearance.

## Run

From the repository root (Windows is required for WPF recipes):

```powershell
dotnet test tests/Sunmao.Recipes.Wpf.Tests/Sunmao.Recipes.Wpf.Tests.csproj --filter FullyQualifiedName~SnapshotRecipeTests
```

The test project compiles [Example.cs](Example.cs) directly; there is no copied implementation.
Tests cover the observable behavior and cleanup. See [the catalog](../../agent/capabilities.json)
for source contracts and target frameworks. These examples are development assets, not shipped APIs.
