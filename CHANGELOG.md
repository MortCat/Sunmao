# Changelog

Versions follow [SemVer](https://semver.org/). Every breaking change documents its migration.

## [Unreleased]

### Added
- Mathematical foundation (shared package version 0.2.0): dependency-free Sunmao.Numerics with
  finite Vector3d, normalized Rotation3d and RigidTransform3d; explicit coordinate/numerical
  contracts, dual-framework tests and a compiled coordinate-transform recipe. Existing APIs are
  unchanged. ADR 0002 records the limited foundation-first admission exception.
- N3 development assets: a versioned capability catalog and deterministic retrieval index, five
  directly compiled recipes with dual-target tests, and fixed verification profiles with JSON
  reports, retained logs, bounded child execution and failure/timeout tests. Recipes participate in
  architecture checks. CI uses the same full profile and retains its diagnostic artifacts.
- P0/P1 foundations: the reproducible SDK baseline now uses the installed 10.0.300 SDK, the
  application template is available under `templates/sunmao-app`, communication contract tests cover
  frame limits, cancellation, timeout and serialization, and Serial settings have a dedicated test
  project with hardware tests excluded from CI.
- N1/N2 hardening: the generated WPF application now owns an awaitable shutdown sequence and tests
  deferred window close on both Windows target frameworks; request/response tests use manual time
  and explicit interleavings; A1 verifies package test projects; and the Windows verification workflow
  and isolated template verifier are available.
- Repository skeleton: shared build settings, central package versions, architecture checks
  (`tools/verify_architecture.py`), development rules (`AGENTS.md`).
- `Sunmao.Core`: `AppComponentBase`, `ComponentLifecycleCoordinator`, `PollingTaskBase` (with
  `delayFirstPoll`), `ExponentialBackoff`, `ThrottledFailureLog`, `ILogSink`.
- `Sunmao.Testing`: `TestWait`, `ManualTimeProvider` (timers fire on `Advance`),
  `SingleThreadUiTestHost`, `RecordingLogSink`.
- `Sunmao.Diagnostics`: `AsyncTextLogSink` (bounded, non-blocking, one file per local day) and
  `LogFileDestination`.
- `Sunmao.Wpf`: `ViewModelBase`, `RelayCommand`, `AsyncRelayCommand` (re-entry guard, owner drain),
  `UiPollingTimer` / `ManualUiPulseDriver` / `InactiveUiPollingSource`, `UiDispatcher`,
  `IConfirmationDialogService`.
- `Sunmao.Wpf.Theme`: `SunmaoTheme` (Walnut Light/Dark, High Contrast, System, Standard/Touch density, custom
  accent), `ThemeManager`, control styles, 40+ stroked icons, lookless controls (`IconGlyph`,
  `StatusChip`, `ConnectionPill`, `InfoCard`, `MetricTile`, `NoticeBar`, `PageHeader`, `EmptyState`,
  `ListRow`, `CheckRow`, `DropZone`) and `ConfirmationDialogService`.
- `samples/Sunmao.Samples.ThemeGallery`: interactive gallery with PNG export; screenshots in
  `docs/images/theme-gallery`.
- `Sunmao.Communication`: `IByteTransport`, `TcpTransport`, `UdpTransport`, `DelimiterFrameDecoder`,
  `LengthPrefixFrameDecoder`, `RequestResponseChannel` (serialized exchanges, abandon disconnects) and
  `ManagedConnection` (supervisor with immediate retry, exponential backoff, throttled log).
- `Sunmao.Communication.Serial`: `SerialTransport` and `SerialPortSettings` over `System.IO.Ports`.
