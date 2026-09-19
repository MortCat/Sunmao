# ADR 0001: Separate the neutral foundation from domain extensions

Status: Proposed, 2026-09-18. Not an accepted change to AGENTS.md.

## Context and explicit conflict

The requested long-term scope includes manipulation, mobile systems, perception, calibration and
localization. AGENTS.md currently prohibits application or domain vocabulary throughout library
code, samples and documentation, and restricts additions to demonstrated reusable capabilities.

Putting these domain capabilities directly into the existing foundation would conflict with that
scope. This decision record explicitly describes the proposed boundary; it does not authorize
domain implementation or silently relax the current rule. The vocabulary in this proposal is
necessary to identify the scope conflict, not an example to copy into neutral foundation APIs.

## Proposed decision

Retain the existing neutral foundation and its dependency direction. Allow separately scoped
domain extensions only after their requirements and location have been established. An extension
may depend on the foundation; the foundation must not depend on domain extensions or their SDKs.

Keep application composition, product workflows and concrete deployment choices in consuming
applications and templates. Keep optional third-party/native backends in adapter packages with
explicit deployment and ownership contracts. Keep local-agent tooling outside runtime Core.

Two deployment choices remain possible:

| Choice | Benefit | Cost |
|---|---|---|
| Separate extension repository | Preserves the current repository-wide neutral scope | Coordinated versions and cross-repository consumer testing |
| Explicitly scoped extension area in this repository | Shared development and verification | Requires changes to AGENTS.md, dependency policy and scoped documentation rules |

Recommended default for planning: preserve the neutral repository while validating domain work in
consuming projects. Choose extension packaging after the two real project requirement records
exist. A future decision may select the same-repository option; this proposal does not select it.

## Acceptance and implementation requirements

Before creating a domain package, record the selected location, concrete reuse evidence, allowed
dependencies, public vocabulary and ownership. If it lives here, update AGENTS.md and the
architecture dependency policy in the same implementation change. Do not add generic names merely
to conceal domain-specific behavior or evade the scope rule.

Keep current lifecycle, concurrency, deterministic-test and versioning requirements. Any change
to those requirements needs its own explicit rationale rather than being implied by this ADR.

## Consequences

The foundation can remain small and broadly reusable while domain contracts remain precise.
Initial domain work may temporarily duplicate code in applications; extraction follows evidence.
Additional packaging and integration checks become necessary once extension boundaries are chosen.

See the [development plan](../development-plan.md) for stages and acceptance gates.
