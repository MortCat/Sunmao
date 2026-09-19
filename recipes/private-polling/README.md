# private-polling

Own periodic bounded work inside a component.

Lifecycle: initialize the owner, then start it. The first iteration waits one second.

Threading: only the private poller writes the count; readers use Interlocked.

Cancellation: each bounded iteration observes the poll token. Stop cancels and drains the poller.

Errors: the base poller logs iteration failures using the supplied sink and its default error policy.

Ownership: the component owns exactly one private Worker; no caller can start that Worker directly.

Cleanup: stop drains work; disposal releases the poller. Tests prove no timer remains after stop.

TimeProvider.System can be used by consumers; the executable test uses ManualTimeProvider.

## Run

From the repository root (Windows is required for WPF recipes):

```powershell
dotnet test tests/Sunmao.Recipes.Tests/Sunmao.Recipes.Tests.csproj --filter FullyQualifiedName~PollingRecipeTests
```

The test project compiles [Example.cs](Example.cs) directly; there is no copied implementation.
Tests cover the observable behavior and cleanup. See [the catalog](../../agent/capabilities.json)
for source contracts and target frameworks. These examples are development assets, not shipped APIs.
