# reconnecting-communication

Own and supervise reconnecting transport lifecycle.

Lifecycle: initialize then start the owner. Start schedules supervision; it does not mean connected.

Threading: status is an immutable snapshot. This recipe performs no request/response I/O.

Cancellation: component start cancels startup; component stop cancels and drains connection attempts.

Errors: a failed attempt is logged and retried with backoff; inspect Snapshot.LastError.

Ownership: the component owns ManagedConnection, which owns the supplied transport.

Cleanup: stop the supervisor before disconnecting, then dispose the connection and its transport.

A scripted transport fails once and reconnects under manual time. This is not a network simulator.

Consumers adding a request channel must drain requests before stopping this owner.

## Run

From the repository root (Windows is required for WPF recipes):

```powershell
dotnet test tests/Sunmao.Recipes.Tests/Sunmao.Recipes.Tests.csproj --filter FullyQualifiedName~ConnectionRecipeTests
```

The test project compiles [Example.cs](Example.cs) directly; there is no copied implementation.
Tests cover the observable behavior and cleanup. See [the catalog](../../agent/capabilities.json)
for source contracts and target frameworks. These examples are development assets, not shipped APIs.
