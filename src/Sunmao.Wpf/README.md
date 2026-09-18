# Sunmao.Wpf

MVVM basics for WPF and a single UI pulse: view models read immutable back-end snapshots on the UI
thread at a steady rate instead of subscribing to back-end events. The look lives in
`Sunmao.Wpf.Theme`; this package contains no styles.

## When to use

| Need | Use |
|---|---|
| A view model base | `Mvvm.ViewModelBase` (`SetProperty`) |
| Button commands | `RelayCommand` / `RelayCommand<T>`; asynchronous work uses `AsyncRelayCommand` / `AsyncRelayCommand<T>` |
| Pages that refresh back-end state periodically | the composition root creates one `Polling.UiPollingTimer` and passes it to view models as `IUiPollingSource` |
| Driving refreshes in tests or without a UI | `Polling.ManualUiPulseDriver` (call `Pulse()`) or `InactiveUiPollingSource` |
| Touching the UI from a background thread | `Threading.UiDispatcher` (rarely; refreshes normally use the UI pulse) |
| Confirming an irreversible action | depend on `Dialogs.IConfirmationDialogService`; the themed implementation is in `Sunmao.Wpf.Theme` |

## Minimal example

```csharp
// Composition root (UI thread)
UiDispatcher.Initialize(Application.Current.Dispatcher);
var pulse = new UiPollingTimer(hertz: 10, log);
var page = new SensorPageViewModel(sensorComponent, pulse);
pulse.Start();

// View model
public sealed class SensorPageViewModel : ViewModelBase, IDisposable
{
    private readonly IUiPollingSubscription _polling;
    private int _value;

    public SensorPageViewModel(SensorComponent sensor, IUiPollingSource pulse)
    {
        _polling = pulse.Register(nameof(SensorPageViewModel), _ => Value = sensor.LastValue);
        RefreshCommand = new AsyncRelayCommand(ct => sensor.RefreshAsync(ct), onError: ShowError);
    }

    public int Value { get => _value; private set => SetProperty(ref _value, value); }
    public AsyncRelayCommand RefreshCommand { get; }

    public void OnActivated() => _polling.Enable();      // refresh only while the page is visible
    public void OnDeactivated() => _polling.Disable();
    public void Dispose() => _polling.Dispose();
}
```

## Contract

- UI pulse callbacks only project snapshots, synchronously and briefly: no I/O, no commands, no
  waiting.
- Snapshots must not overwrite what the user is editing, selecting or scrolling.
- `AsyncRelayCommand` disables itself while running and ignores re-entry. When a page closes, its
  owner calls `CancelAndWaitAsync`.
- `UiPollingTimer` and `ManualUiPulseDriver` belong to the thread that created them; calls from
  other threads throw.

## Do not

- Give view models threads, `Task.Run`, timers, `CancellationTokenSource`s, channels or locks, or
  derive them from `PollingTaskBase`.
- Call `MessageBox`, `Application.Shutdown` or `Dispatcher.Invoke` from a view model.
- Create more than one `UiPollingTimer` per application.
