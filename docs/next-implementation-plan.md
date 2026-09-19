# Next implementation plan

Status: N1/N2 core changes and N3 development assets implemented, 2026-09-19. The remaining
N1/N2 acceptance gaps below and N4 onward remain open. This document does not authorize publication. See
[development-plan.md](development-plan.md)
for the long-term stages and [ADR 0001](decisions/0001-extension-boundaries.md) for scope boundaries.

## Outcome

Priority update, 2026-09-19: the maintainer has requested direct expansion of reusable computation.
The first increment is the bounded numerical foundation in
[the mathematical plan](mathematical-foundations-plan.md) and ADR 0002. This overrides the earlier
requirement-record-first ordering for that increment only; N1/N2 gaps remain open.

Produce a reliable application example and a small set of executable development assets that a
local model can retrieve, compose and verify. In parallel, collect real consumer requirements for
the first shared data capability. Keep agent tooling outside runtime packages.

## Evidence and remaining gaps

The current implementation has successful dual-framework builds and repository tests, plus four
generated application tests per Windows framework. The generated tests now cover component polling,
refresh, idempotent view-model disposal and deferred WPF close. The repository still needs the
remaining communication matrix and a hosted CI result before N2 is complete.

| Source | Observation | Next action |
|---|---|---|
| `templates/sunmao-app/content/Sunmao.App/App.xaml.cs`, `MainWindow.xaml.cs` | Implemented: owned startup/shutdown tasks, close interception, cleanup ordering and generated WPF close test. | Add failure injection and repeated-close assertions in a later hardening pass. |
| `templates/sunmao-app/content/Sunmao.App/DeviceComponent.cs` | Implemented: sequence-aware compare-exchange prevents an older concurrent publication from replacing a newer snapshot. | Add a deterministic forced interleaving test for the publication helper. |
| `templates/sunmao-app/content/Sunmao.App/MainWindow.xaml` | Implemented: timestamp uses an explicit OneWay binding. Theme selection remains a later example improvement. | Add an observable theme selection control or correct the example promise. |
| `tests/Sunmao.Communication.Tests/RequestResponseChannelTests.cs` | Implemented: manual timeout, explicit response signals, queued cancellation, exchange cancellation, partial-frame reset and forced pending serialization. | Add UDP buffer-boundary and remaining connection-stop/recovery scenarios. |
| `tests/Sunmao.Communication.Serial.Tests/SerialPortSettingsTests.cs` | Implemented: unavailable-port check is clearly platform-specific and skipped in CI. | Keep hardware validation outside CI and document equipment when a real device test is added. |
| `tools/verify_architecture.py` | Implemented: A1 checks the required test project and the self-test seeds that failure. | Extend catalog/stale-capability checks with N3. |
| `.github/workflows/verify.yml` | Implemented: Windows workflow runs restore, build, test, architecture checks, template verification and diff checks. | Record the first hosted CI result. |
| `tools/verify_template.ps1`, template README | Implemented: solution restore, isolated package cache/source, package-version validation and explicit generated paths. | Consider isolated package-cache cleanup/reporting on failure and add a generated subprocess smoke check. |

## Delivery order

| Increment | Priority | Deliverable | Dependency | Completion gate |
|---|---|---|---|---|
| N1 | First | Correct application lifecycle, snapshots and UI example | Existing template | Deterministic lifecycle tests and a generated application exit test pass on both Windows targets |
| N2 | First | Communication failure contracts and reproducible verification | N1 for final template gate | Meaningful forced-interleaving tests, architecture self-tests and clean generated builds pass |
| N3 | Next | Capability catalog, compiled recipes and a structured verification command | N1/N2 verified capabilities | Every advertised entry resolves to verified source, a recipe and a test; failed checks cannot report success |
| N4 | Parallel | Two real consumer requirement records | Consumer details | Shared need, differences, datasets and measurable acceptance criteria are documented |
| N5 | After N3 | Local-model evaluation pilot | Named model/runtime and available hardware | Reproducible paired results, including failures, are recorded |
| N6 | After N4 | First reusable data capability | Reuse evidence and applicable scope decision | Small API is used and tested by its intended consumers |

Finish each increment with a reviewable change and its evidence. N4 does not block N1-N3.
Do not mark all of P0/P1 complete merely because N1 passes.

## N1: Make the application example trustworthy

Keep shutdown coordination in the template. Use explicit application shutdown and an owned,
awaitable close operation: stop accepting commands, disable projections, cancel and await active
commands, stop the UI pulse, dispose components, drain diagnostics, then finalize shutdown.
Route startup failure through the same cleanup owner. Define repeat-close and cleanup-failure
behavior, and ensure later cleanup still runs when an earlier stage fails. Do not treat an
`async void` override as an awaitable shutdown barrier.

For snapshot publication, evaluate a single writer or compare-and-exchange publication that cannot
replace a newer sequence with an older one. Record the selected ordering semantics and prove them
with explicit interleavings. Preserve the rule against lifecycle-gate reentry from callbacks.

Acceptance scenarios:

- Close while a command is pending: disposal and process exit wait for its cancellation/drain.
- Repeated close: cleanup executes once; startup failure releases initialized resources.
- One cleanup failure: remaining resources still receive cleanup; the failure is observable.
- Poll and refresh overlap: published sequence never regresses; stop leaves no active poll timer.
- Timestamp binding updates; promised theme selection has an observable effect.

Test ownership logic with explicit signals and `SingleThreadUiTestHost`; add a bounded generated
application subprocess check for actual shutdown. Component-only tests are not sufficient evidence
for application exit. Update concurrency baseline explanations only after reviewing the new owner.

## N2: Close verification gaps

Replace real-time fake polling with a bounded, signal-driven test transport. Force the first
request to remain pending before submitting a second request. Inject a late response from the
abandoned connection and assert it cannot satisfy the new exchange. Separately cover queued
cancellation, cancellation during exchange, stop during connect, retry/recovery, disposal and UDP
datagram/insufficient-buffer behavior. Record any platform differences instead of assuming one
network behavior everywhere. Document the owner of `ManagedConnection` and its stop order.

Make template verification build the selected configuration before packing, use a private package
cache, derive or validate package versions, and propagate configuration to generated builds/tests.
Verify both Windows target frameworks from outside the repository. Run architecture checks with
an explicit generated-template policy; scanning placeholder source alone is not that check.
Keep useful failure logs and validate exact owned temporary paths before cleanup.

Add Windows CI using `global.json` and the repository definition of done, plus template verification.
Use seeded architecture failures to prove missing test projects are detected. Record compiler-server
workarounds as environment settings. Add non-Windows checks only for explicitly supported general
package targets, with their actual test/platform requirements documented.

## N3: Development assets for a local model

Implemented files and contracts:

| Artifact | Minimum content | Validation |
|---|---|---|
| `agent/capabilities.json` and schema | Schema version; stable capability ID; package/API version; maturity; target frameworks; contract/source paths; recipe ID; verification profile; limitations | Reject duplicate IDs, missing paths, version drift and unsupported profile references |
| `recipes/` | Five small runnable examples: lifecycle composition, private polling, reconnecting communication, command cancellation and snapshot UI | Compile supported targets; verify observable success, failure and cleanup behavior |
| `tools/verify.ps1` | Fixed verification profiles; structured JSON results; logs; per-step exit code, elapsed time and status | Inject command failure and timeout; nonzero aggregate result; skipped/blocked steps never count as passed |
| `agent/index.json` | Deterministic mapping from capability IDs to short contracts and recipe paths | Rebuild from catalog; reject stale paths and source versions |

Start with file-based lookup and a CLI. Catalog entries select predefined verification profiles,
not arbitrary executable commands. Specify a JSON output schema with run identity, source version,
environment, step results and diagnostic paths. Bound child execution and drain/terminate owned
processes on cancellation. Keep verification output distinct from model-generated prose.

Each recipe documents required lifecycle state, threading, cancellation, errors, ownership and
cleanup. Use the corrected template as one source of examples. Avoid introducing a new runtime
abstraction just to simplify retrieval. Neither a model server nor an agent transport is selected
by this plan.

The five example-backed entries now resolve to directly compiled recipe source and executable tests.
The general recipe project targets net8.0/net10.0; the WPF recipe project targets their Windows
variants. The catalog checker rejects duplicate IDs and JSON keys, missing/out-of-root paths,
version/framework drift, unsupported profiles, stale hashes, uncompiled recipes and missing test classes.
The fixed catalog/recipes/full profiles retain JSON reports and logs under artifacts/verification.
Injected failure, missing-command, timeout, descendant-process termination and cancellation tests
guard the runner's aggregate result. Recipes are included in architecture rules with seeded violations.
See [the agent guide](../agent/README.md) for invocation, schema and process-ownership limits.

## N4/N6: Consumer requirements and first extraction

Create `docs/requirements/project-a.md`, `project-b.md` and `shared-capabilities.md` when actual
consumer inputs are available. Keep missing fields explicitly unknown. Needed inputs are:

- Purpose and first end-to-end workflow of each real project.
- OS/architecture, deployment mode, UI and .NET requirements.
- Data sources and existing SDKs; sample payloads; rates, sizes, units and coordinate conventions.
- Timestamp origins, clock relationships, buffering/lifetime requirements and replay datasets.
- Latency, memory and accuracy targets with measurement method and expected results.
- Shared need and meaningful differences between the consumers.

Use those records to select exactly one first capability. Candidate order for investigation is
timestamped metadata and bounded delivery, recording/replay, then spatial transformations or
image/point buffer descriptors as the evidence warrants. This is an investigation order, not a
commitment to create four packages. Compare existing representations before defining public types.

For domain work, resolve the proposed ADR's repository/package boundary before adding domain code.
Keep the foundation dependency direction intact. New public API requires the repository's minor
version bump; breaking semantics require its major-version and migration process.

## N5: Measure local-model usefulness

Begin with the five recipe task families, at least one independent held-out acceptance scenario per
family, and the same task inputs for runs with and without the development assets. Keep acceptance
tests out of retrieved example material. Use a fixed attempt/time/tool budget and record exact model,
quantization, runtime, hardware, context and sampling settings. Gemma 12B is a user-supplied candidate,
not a selected or evaluated configuration.

Measure first-build success, independent test success, architecture violations, repair attempts and
elapsed time. Record failed and budget-exhausted runs. A small pilot establishes a baseline; it does
not support a broad quality guarantee. Runtime selection, downloads and actual evaluation await
machine/model requirements; catalog and recipe work can proceed independently.

## Verification and handoff

Implementation increments run the repository definition of done, plus relevant template, subprocess,
recipe or verification-runner checks. Report locally passed checks separately from CI not yet run,
hardware skips, unavailable model runs and unimplemented requirements. N3 adds no runtime package
or public library API; the later numerical foundation adds API and advances the shared version to 0.2.0.

Next scope: close the remaining N1/N2 acceptance gaps, collect N4 consumer inputs, then prepare N5's
independent evaluation tasks. Hosted CI and actual local-model quality measurements are still pending.
Report completion per increment rather than per broad roadmap stage.
