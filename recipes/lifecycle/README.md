# lifecycle

Compose component lifecycles in dependency order.

Lifecycle: initialize and start source before consumer; stop and dispose in reverse order.

Threading: one composition-root caller; hooks must not reenter the lifecycle gate.

Cancellation: the caller token reaches initialization, start and explicit stop. Disposal always runs.

Errors: initialization/start errors propagate; coordinator cleanup continues across component failures.

Ownership: the coordinator owns both supplied components. Do not dispose them independently.

Cleanup: await the coordinator's disposal even when startup fails.

The recipe immediately stops after startup so its executable test has a bounded workflow.

## Run

From the repository root (Windows is required for WPF recipes):

```powershell
dotnet test tests/Sunmao.Recipes.Tests/Sunmao.Recipes.Tests.csproj --filter FullyQualifiedName~LifecycleRecipeTests
```

The test project compiles [Example.cs](Example.cs) directly; there is no copied implementation.
Tests cover the observable behavior and cleanup. See [the catalog](../../agent/capabilities.json)
for source contracts and target frameworks. These examples are development assets, not shipped APIs.
