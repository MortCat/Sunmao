# Sunmao.Numerics

Dependency-free, double-precision 3D value operations for coordinate calculations.
Targets net8.0 and net10.0. It references neither Core nor any third-party library: calculations
have no lifecycle, clock, I/O, worker or UI dependency.

## When to use

| Need | Type |
|---|---|
| Finite coordinates, dot/cross products and robust direction normalization | Vector3d |
| Normalized orientation, axis-angle construction, composition and angular comparison | Rotation3d |
| Map points/directions between coordinate systems and invert or chain mappings | RigidTransform3d |

Use this package when double precision and these explicit conventions match your inputs.
The first release is example-backed; application-specific accuracy and throughput must be measured.
The [admission decision](../../docs/decisions/0002-explicit-mathematical-foundations.md) records the
scope exception, representation comparison and primary references.

## Minimal example

```csharp
using Sunmao.Numerics;

var aFromB = new RigidTransform3d(
    Rotation3d.FromAxisAngle(Vector3d.UnitZ, Math.PI / 2),
    new Vector3d(10, 0, 0));
var bFromC = new RigidTransform3d(Rotation3d.Identity, new Vector3d(2, 0, 0));
var aFromC = aFromB * bFromC;
var pointInA = aFromC.TransformPoint(Vector3d.UnitX); // Approximately (10, 3, 0).
var pointInC = aFromC.Inverse().TransformPoint(pointInA);
```

The [compiled recipe](../../recipes/coordinate-transform/README.md) checks this known result and
the inverse mapping on both target frameworks.

## Contract

- Values are immutable and safe to share. Methods are synchronous, bounded and require no lifecycle
  or cancellation token. A read-modify-write replacement of shared values still needs caller coordination.
- Right-handed active rotations; angles in radians; column-vector action. (A * B) applies B first.
  Quaternion order is (X, Y, Z, W), with W the scalar part.
- Translations and points use one consistent caller-selected unit. Frame labels and units are not
  stored or validated. TransformDirection applies rotation only; TransformPoint also adds translation.
- Default Vector3d is zero. Default Rotation3d and RigidTransform3d are identity.
- Quaternion creation normalizes finite nonzero input and canonicalizes its sign. Exact component
  equality handles identical sign-equivalent inputs, but rounded calculations need tolerance checks.
  AngularDistance returns the shortest separation in radians, including small angles.
- Vector normalization scales before measuring the norm, including subnormal inputs. Zero has no
  direction: TryNormalize returns false/zero and Normalize throws InvalidOperationException.
  Axis-angle creation rejects a zero axis even when the angle is zero.
- Non-finite scalar/component input throws ArgumentOutOfRangeException. A zero quaternion or zero
  axis throws ArgumentException. Division by zero throws ArgumentOutOfRangeException.
- Arithmetic that becomes non-finite throws ArithmeticException, including an unrepresentable length.
  Underflow may round to zero; cancellation and accumulated rounding remain possible. General dot,
  cross, addition and transform calculations are not compensated or arbitrary precision.

## Do not

- Mix degrees with radians, inconsistent length units or mismatched coordinate frames.
- Treat transform multiplication as commutative or add translation to a direction.
- Use exact equality to judge measured-data accuracy; choose tolerances for your scale and units.
- Treat this package as a matrix decomposition, optimization, trajectory or hardware-control library.
- Infer timing guarantees or validated end-to-end consumer accuracy from deterministic unit tests.
