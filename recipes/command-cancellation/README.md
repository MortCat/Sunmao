# command-cancellation

Cancel active commands and await cleanup on close.

Lifecycle: construct on the UI thread; execute while active; await disposal when closing.

Threading: the owner and its availability flag are UI-thread-only.

Cancellation: the operation must observe its token; CancelAndWaitAsync waits through its cleanup.

Errors: command failures go to the supplied onError callback; cancellation is a normal completion.

Ownership: the owner holds one command and disables new work before requesting cancellation.

Cleanup: await disposal before releasing dependencies used by the operation.

The executable test holds cleanup behind an explicit signal to prove closing actually waits.

Command is public so a Button can bind to it; the test checks that binding resolves to the
owned command before exercising the failure callback.

## Run

From the repository root (Windows is required for WPF recipes):

```powershell
dotnet test tests/Sunmao.Recipes.Wpf.Tests/Sunmao.Recipes.Wpf.Tests.csproj --filter FullyQualifiedName~CommandRecipeTests
```

The test project compiles [Example.cs](Example.cs) directly; there is no copied implementation.
Tests cover the observable behavior and cleanup. See [the catalog](../../agent/capabilities.json)
for source contracts and target frameworks. These examples are development assets, not shipped APIs.
