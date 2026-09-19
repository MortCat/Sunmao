# Mathematical foundations and reusable operations

Priority update: reusable computation is the next expansion focus, following the maintainer's
clarification on 2026-09-19. The existing development assets remain the delivery mechanism for
new APIs. Consumer-specific validation and model evaluation remain useful follow-up work.

## First increment

Sunmao.Numerics 0.2.0 introduces Vector3d, Rotation3d and RigidTransform3d, covering double-precision
vector operations, orientation and coordinate composition/inversion. It is independent of Core
and external packages. [ADR 0002](decisions/0002-explicit-mathematical-foundations.md) records its
bounded admission exception and the numerical conventions.

The increment includes XML contracts, invalid-input behavior, deterministic numerical tests and a
directly compiled recipe in the capability catalog. It does not claim production consumer evidence.

## Candidate increments after the first

These are an investigation sequence, not available APIs or approval for an entire new framework.
Keep each increment narrow, compare existing implementations, and apply the admission rules.

| Area | Candidate operations | Acceptance questions before implementation |
|---|---|---|
| Linear algebra | Small matrices, linear solves, least squares | Shapes, conditioning, rank deficiency, dependency/backend choice and residual tolerances |
| Interpolation | Scalar/vector interpolation, orientation interpolation, time-indexed samples | Endpoint rules, continuity, duplicate times and extrapolation |
| Estimation | Descriptive statistics, filtering, uncertainty propagation | Noise assumptions, state ownership, sample intervals and reproducible datasets |
| Discrete planning | Graph search and cost evaluation | Edge constraints, unreachable goals, cancellation, memory limits and deterministic tie breaking |
| Data processing | Timestamped samples, bounded delivery, recording and replay | Clock domains, buffer ownership, capacity, overflow and persistence compatibility |
| Execution support | Job states, sequencing, timeout/retry policies and diagnostics | Reuse existing lifecycle/polling primitives; define cancellation, recovery and stop order |

Application composition and device-specific operations stay outside these mathematical primitives.
Domain extensions still need the boundary decision described in ADR 0001.

## Delivery gate

Each implemented increment needs a bounded API, explicit units/conventions, analytical fixtures,
failure cases, both target-framework tests, a minimal compiled recipe, catalog entries and the full
verification profile. New API increments the minor version and updates templates and metadata.
Remaining N1/N2 acceptance gaps stay tracked; they are not declared complete by adding mathematics.
