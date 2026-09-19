# Sunmao development plan

Status: N1/N2 core changes and N3 development assets implemented; remaining acceptance gaps are tracked in the
[next implementation plan](next-implementation-plan.md), 2026-09-19. P2 requires real project inputs.
The subsequent maintainer priority update begins reusable computation with the narrowly scoped
Sunmao.Numerics foundation; see [the mathematical plan](mathematical-foundations-plan.md) and ADR 0002.

This plan translates the repository review into deliverables and acceptance gates. It is not a
catalog of available features. Package names and reference projects below are candidates, not
commitments to add public APIs. No implementation starts solely because an item appears here.

## Objective and constraints

Build reusable, tested capabilities for multiple projects, with sufficiently explicit contracts,
examples and verification tools that a local small-model development agent can assemble reliable
applications. Evaluate model performance instead of assuming a model size guarantees quality.

Keep the foundation domain-neutral and dependency-light. Extract shared capabilities from proven
use or requirements shared by at least two real projects. Synthetic reference scenarios validate
behavior but do not, by themselves, satisfy the two-project requirement in AGENTS.md.

Domain-specific expansion requires the scope decision in
[ADR 0001](decisions/0001-extension-boundaries.md). Existing repository rules remain in force.
Application composition stays in templates or consuming applications. General libraries remain
independent of WPF. No hard real-time behavior is promised by the existing polling infrastructure.

## Review baseline

The initial implementation batch contains seven library projects and seven test projects.
Lifecycle, polling, diagnostics, TCP/UDP, serial transport, WPF behavior and themes exist. Modbus
is a planned extension, not an available package. Communication and serial contract tests run
without hardware, and `templates/sunmao-app` generates a tested WPF application for both supported
Windows target frameworks. N1 now covers awaitable application shutdown and generated WPF close
behavior; N2 adds isolated template verification, CI and stronger request/response tests.
N3 adds five directly compiled recipes, two recipe test projects, a versioned capability catalog,
a deterministic retrieval index and a structured verification runner. These are development assets;
they neither add runtime packages nor establish measured local-model performance.

The repository pins SDK 10.0.300 in `global.json` because that is the reproducible SDK available in
the current development environment. The verification commands below use single-process,
non-shared-compilation flags locally when the environment's compiler server cannot be reached; the
project configuration and target frameworks are unchanged.

## Delivery sequence

| Stage | Priority | Deliverable | Depends on | Completion gate |
|---|---|---|---|---|
| P0 | First | Reproducible baseline and accurate documentation | None | Full repository verification passes on a documented environment |
| P1 | First | Communication confidence and one working application template | P0 | Failure scenarios and generated application tests pass |
| P2 | Next | Two real project requirement records and executable reference scenarios | P1; project inputs | Shared requirements and measurable acceptance criteria are recorded |
| P3 | Next | Small shared capability increments | P2; scope decision where needed | Each increment is consumed and verified in its intended projects |
| P4 | Parallel after P1 | Recipes, capability catalog and local-agent evaluation | P1; extend with P3 | Versioned benchmark reports show actual agent outcomes |
| P5 | Later | Additional backends and domain capabilities | Relevant P3 increments | Compatibility, deployment and performance evidence is recorded |

Use completion gates rather than calendar promises. Estimate individual increments after their
inputs, platform constraints and acceptance datasets are known. P4 can begin with existing APIs;
it does not need to wait for domain algorithms.

## P0: Establish a trustworthy baseline

1. Recheck the available SDKs and choose a reproducible supported SDK configuration. Document the
   decision; do not silently downgrade target frameworks or bypass global.json for release checks.
2. Correct the root package map and template description to distinguish available, experimental
   and planned features. Reserve Modbus implementation for a proven project requirement.
3. Run the full build and tests. Record actual failures separately from environment restrictions.
4. Document the ownership role of ManagedConnection relative to AppComponentBase. Review existing
   lifecycle exceptions before treating them as patterns for generated code.
5. Extend architecture checks to detect missing package test projects and stale capability entries.
   Keep checks precise and add seeded self-tests. Do not claim text scanning proves concurrency
   correctness or covers all AGENTS.md rules. The missing-test-project check and seeded case are now
   implemented.
6. Add a repeatable CI workflow using the selected SDK, both target frameworks and Windows for WPF.
   Include non-Windows verification of general packages when that platform is a supported target.
   The Windows workflow is now present; hosted execution remains a CI result rather than a local
   result.

Acceptance: every published capability resolves to existing code and a usable example; the full
verification command set passes; CI and local verification use the same documented configuration.

## P1: Strengthen existing capabilities and provide a template

### Communication verification

Add deterministic scenarios for:

- Split/coalesced frames, length prefixes, invalid lengths and frame-size limits.
- Timeout, cancellation while queued, cancellation during an exchange and late replies after an
  abandoned exchange. Verify the next request cannot consume a previous response.
- Connection failures, retry progression, recovery, stop during connect and resource disposal.
- UDP datagram boundaries and behavior when the receive buffer is insufficient.
- Concurrent callers and the documented boundary between active requests and shutdown.
- Serial configuration validation and testable cancellation/resource behavior. Keep hardware-only
  tests explicitly skipped with required equipment recorded; CI must not depend on physical ports.

Use ManualTimeProvider, explicit signals and in-memory fakes where possible. Retain a small set of
loopback integration tests. If an API is hard to test, first identify the smallest internal seam;
do not introduce public abstractions solely to mock implementation details.

Acceptance: each specified contract has an observable assertion, failure paths release resources,
and timing tests do not rely on fixed sleeps. Document unresolved contracts before changing them.

### Application template

Create templates/sunmao-app with a composition root, a neutral DeviceComponent owning a private
poller, a single status page, logging, theme switching, and a deterministic test project. Use a
simulated device by default so first run requires no hardware. Keep PageViewModelBase and navigation
outside library packages; application-specific services can be added by the consuming project.

Make initialization, startup failure, command cancellation, shutdown and disposal visible in the
example. Define a clean-checkout dependency strategy for generated projects: local packages for
template verification and a documented package source for consumers. Do not reference unpublished
versions as though they were obtainable from a public feed.

Acceptance: instantiate into a temporary directory outside the source tree, restore, build and test
on both configured Windows target frameworks. Verify component start, polling, refresh and clean
shutdown in the generated deterministic test project. Architecture checks cover the generated
application.

The repository helper `tools/verify_template.ps1` restores the solution, packs the current libraries
into a private source and cache, installs the template into a private template hive and verifies
generated test projects for both Windows target frameworks. It is an additional P1 check; consumers
still need a package source containing the required Sunmao packages.

## P2: Select two real projects and define reference scenarios

The following scenarios are proposed test vehicles. Actual project names, equipment, platforms,
datasets and deployment constraints have not been supplied. Keep domain-specific implementations
in consuming projects until their location and scope are settled.

| Scenario | Minimal flow | Shared requirements to investigate | Acceptance evidence |
|---|---|---|---|
| A: Capture and transformation estimation | Replay paired observations, estimate a transform, report quality, persist the result | Frame identifiers, units, timestamps, buffer ownership, serialization and cancellation | Known-transform fixtures; held-out observations; explicit invalid/degenerate input results |
| B: Replay and grid-based route computation | Replay observations and poses, update a grid, compute a route, inspect the result | Time alignment, transforms, bounded processing, replay, versions and diagnostics | Known maps; unreachable targets; stale observations; route validity against the declared model |

For A, record measurement noise, precision requirements, transform direction, sample diversity,
calibration mode and quality thresholds before implementation. Offline estimation is separate from
any automated physical sample-collection procedure.

For B, record grid resolution, unknown-cell policy, footprint, movement constraints, map version and
planning budget. A geometric route alone does not prove that a device can execute it.

Create one requirement record per real project containing:

- OS, architecture, UI requirements, deployment mode and supported .NET targets.
- Data sources, protocols, rates, payload sizes, timestamp sources and clock relationships.
- Input/output examples, units, coordinate conventions and expected error conditions.
- Memory, latency and quality budgets, with the target machine and measurement method.
- Available datasets, expected results, vendor dependencies and redistribution constraints.
- Required capability now, possible later capability, and evidence of reuse elsewhere.

Acceptance: the two records identify an actual shared capability and its differences. Numerical
thresholds are chosen from those requirements, not invented as universal library guarantees.

## P3: Extract shared capabilities incrementally

Do not create all candidate packages in advance. For each candidate, first compare existing .NET
and backend types, then justify any Sunmao-owned representation or contract.

| Candidate boundary | Initial responsibility | Required contract |
|---|---|---|
| Geometry / transforms | Only the shared spatial representations and operations required by P2 | Precision, units, handedness, quaternion order, source/target frames, transform composition and invalid inputs |
| Timestamped data / streaming | Bounded delivery and required synchronization policies | Acquisition vs. arrival time, clock identity, sequence, capacity, overflow, cancellation, drain order and failure ownership |
| Recording / replay | Reproducible input and versioned metadata | Format/schema version, data integrity, ordering, seek/reset behavior and replay time |
| Image / point data | Shared descriptors and buffer access where needed | Layout, stride, depth scale, fields, ownership, lifetime, copying and native-memory boundaries |
| Domain extensions | Specific estimation, mapping or planning problems proven in P2 | Input model, quality/status, reproducibility, validity limits and backend-specific failures |

TimeProvider remains the host-side time abstraction. Device clock identifiers and clock mappings
describe external data; they must not become a competing application clock interface. A host clock
does not automatically synchronize devices.

Retain immutable metadata and snapshots, but explicitly manage large-buffer ownership instead of
requiring full image or point-buffer copies for each update. Read-only views alone do not establish
immutability of their backing storage.

Keep optional native or out-of-process backends outside the dependency-free foundation. Evaluate
each integration for platform support, conversion cost, cancellation, process failure, versioning,
license and packaging. No backend is selected by this plan.

For every implementation increment:

1. Link it to the real requirement and document alternatives and the smallest useful API.
2. Define owner, capacity, stop order and failure policy before adding concurrency primitives.
3. Implement one working flow, deterministic tests and a runnable usage recipe.
4. Update dependency policy, package documentation, root package map and release metadata.
5. Validate consumers and measure performance when payload size or timing warrants it.

New public APIs require a minor version bump; breaking changes require a major version bump and
migration notes, as specified by the current repository policy.

## P4: Make verified capabilities usable by local development agents

Start with a development agent that creates and verifies projects. Treat an agent operating a
running device as a separate future workflow with explicit operation boundaries and authorization.

Deliver these assets using existing stable capabilities first:

- A machine-readable capability catalog with capability ID, maturity, package/API version,
  prerequisites, supported platforms, recipe path, verification command and limitations.
- Compilable recipes for lifecycle composition, reconnecting communication, command cancellation,
  snapshot-driven UI and deterministic polling tests. Include failure and cleanup paths.
- A small retrieval index derived from versioned source documentation and recipes. Supply only
  task-relevant material; keep catalog entries consistent with actual package versions.
- One verification entry point with bounded execution and structured build/test/architecture
  results, exit codes and diagnostic locations. Initially expose it through a local CLI; select any
  further agent transport only when its consumer is known.
- A fixed evaluation suite containing at least the five recipe task families, plus independent
  acceptance tests and scenarios not copied from the recipes.

Agent flow: select a capability, retrieve its contract and recipe, generate a small change, run
verification, inspect diagnostics, and repair within a configured iteration/time budget. Exhausting
the budget produces a failure report rather than an unverified success claim.

Evaluate a named local model configuration both with and without these assets on the same tasks.
Record model/version, quantization, runtime, hardware, context limit, sampling settings, attempts,
elapsed time and tool budget. Track first-build success, independent acceptance-test success,
architecture violations and repair attempts. Report sample sizes and per-task failures. Do not set
a universal success-rate promise before obtaining a baseline.

Acceptance: catalog/recipe consistency is automatically checked, recipes pass ordinary repository
verification, and an evaluation report can be reproduced. Any claim that the assets improve model
quality requires measured evidence from the paired evaluation.

## P5: Expand by demonstrated need

Add domain algorithms, device adapters and alternate backends one vertical slice at a time. Keep
different problem models distinct: a grid route, a joint-space trajectory and a spatial trajectory
need not share one planner interface. Separate estimation from physical execution, and planning
from tracking/control. Prefer integrating a suitable tested backend when reimplementation has no
demonstrated project benefit.

Defer universal plugin systems, generic workflow engines, a universal device API, broad protocol
coverage, custom numerical frameworks and model fine-tuning until requirements or evaluation
results justify them.

## First implementation batch

The first batch is P0, followed by the communication tests and template in P1. Its deliverables are:

1. An accurate README and reproducible SDK/build configuration.
2. Complete initial verification results and targeted fixes for reproduced failures.
3. A documented lifecycle ownership convention and prioritized communication contract tests.
4. A dedicated Serial test project with honest hardware limitations.
5. One generated application that builds, tests and shuts down correctly.

This batch does not require the two project choices. Domain extraction in P3 depends on P2 inputs;
candidate scenarios must not silently become invented user requirements.

## Verification and reporting

Implementation batches use the repository definition of done:

```text
dotnet build Sunmao.slnx
dotnet test Sunmao.slnx --no-build
python tools/verify_architecture.py --self-test
python tools/verify_architecture.py --root .
git diff --check
```

Add template instantiation checks, consumer tests or performance measurements when the batch calls
for them. Report passed, failed and blocked checks separately. A planning document does not mark
any implementation stage complete, and an environment blocker does not count as a passing test.
