# ADR 0002: Admit an explicitly requested mathematical foundation

Status: Accepted for this implementation increment, 2026-09-19.

## Context and rule conflict

The maintainer has clarified that the immediate priority is reusable mathematical and general
application capabilities. The previous sequence placed additional verification and two consumer
requirement records before every new capability. AGENTS.md's demonstrated-use/two-project rule
therefore prevents a foundation-first implementation of the requested mathematical scope.

This decision records a narrow admission-policy change for this request. There are no supplied
production consumers and no claim that synthetic examples establish real deployment evidence.
The initial package is example-backed and must be evaluated in actual consumers.

## Decision

Allow Sunmao.Numerics as the explicitly requested, domain-neutral mathematical foundation.
The initial release contains only finite double-precision Vector3d, normalized Rotation3d and
RigidTransform3d, plus deterministic tests and a directly compiled coordinate-transform recipe.
Register this exception in AGENTS.md. Other additions still use the existing admission policy;
this exception does not authorize speculative frameworks, device APIs or domain-specific packages.

The package has no project or third-party dependency. Neither it nor Core references the other.
It is synchronous, immutable and independent of lifecycle, WPF, transport and model runtimes.
Its methods have bounded work and no I/O. Add a minor release (0.2.0) under the repository's shared
version property; update template and capability versions together. No existing API is removed.

## Representation and alternatives

The existing System.Numerics.Vector3 and Quaternion APIs use single-precision components.
Use them when that precision and their conventions fit; do not implicitly convert double inputs
to them. This increment chooses double precision and explicit active column-vector conventions.
It does not claim faster performance than the framework's SIMD-oriented types.

An external numerical backend is not needed for the three small value types. General dense/sparse
linear solves, decompositions and optimization are deliberately not implemented in this increment.
Evaluate existing backends and their deployment/dependency contracts before expanding into those areas.

Define quaternion order as (x, y, z, w), right-handed axis-angle rotation in radians, and transform
action as pA = R_AB pB + t_AB. AFromB * BFromC maps C to A, applying the right operand first.
Directions omit translation. Inversion uses the inverse rotation and rotated negative translation.
Default rotations/transforms are identity; default vectors are zero.

Non-finite inputs and degenerate directions have explicit errors. Normalization scales first to
avoid unnecessary squaring overflow/underflow. General arithmetic can still overflow or lose
precision; this is finite-precision arithmetic, not an arbitrary-precision solver. Exact equality
is distinct from tolerance-based physical or geometric acceptance.

## Evidence and boundary

Use known axis rotations and noncommuting transform chains, not only inverse round trips.
Check default values, quaternion sign, degeneracy, small angles, normalization extremes and
repeated composition. Build and test net8.0/net10.0 and run the full repository verification profile.

ADR 0001 remains proposed for domain extensions. This decision only admits the mathematical package;
it does not settle that separate repository/domain-vocabulary decision.

## Primary references

- [Microsoft Vector3 API](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.vector3?view=net-8.0)
- [Microsoft Quaternion API](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.quaternion?view=net-10.0)
- [Homogeneous transformation composition and inversion](https://modernrobotics.northwestern.edu/nu-gm-book-resource/3-3-1-homogeneous-transformation-matrices/)
