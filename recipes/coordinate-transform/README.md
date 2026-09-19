# Coordinate transform composition

Map a point through two coordinate systems using Sunmao.Numerics. The example is compiled directly
by Sunmao.Recipes.Tests and uses no device, UI, clock or worker.

Lifecycle: none; these are immutable pure values.
Threading: concurrent calls are safe with independently supplied inputs.
Cancellation: not needed for the fixed-size, bounded calculation.
Errors: source value factories reject non-finite/degenerate input; non-finite arithmetic throws
ArithmeticException. Do not silently replace invalid input with identity.
Ownership and cleanup: values own no resources.

Use matching frames and a consistent length unit. AFromB * BFromC applies BFromC first.
The test checks the known point (10, 3, 0) and recovers the input with the inverse.
Frame names and units are caller responsibilities, not validated metadata.

## Run

```powershell
dotnet test tests/Sunmao.Recipes.Tests/Sunmao.Recipes.Tests.csproj --filter FullyQualifiedName~CoordinateTransformRecipeTests
```

See [the source](Example.cs) and [the numerical contract](../../src/Sunmao.Numerics/README.md).
