# Changelog

Versions follow [SemVer](https://semver.org/). Every breaking change documents its migration.

## [Unreleased]

### Added
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
