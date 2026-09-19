using Sunmao.Wpf.Mvvm;
using Sunmao.Wpf.Polling;

namespace Sunmao.App;

/// <summary>Projects Component snapshots onto the UI thread and owns the page command.</summary>
public sealed class DeviceViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly DeviceComponent _device;
    private readonly IUiPollingSubscription _subscription;
    private readonly TaskCompletionSource<bool> _disposeCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string _status = "Starting";
    private string _updatedAt = "—";
    private int _disposeStarted;

    /// <summary>Creates the view model and registers its initially disabled UI projection.</summary>
    public DeviceViewModel(DeviceComponent device, IUiPollingSource pulse)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        ArgumentNullException.ThrowIfNull(pulse);
        _subscription = pulse.Register(nameof(DeviceViewModel), _ => ProjectSnapshot());
        RefreshCommand = new AsyncRelayCommand(
            ct => _device.RefreshAsync(ct),
            onError: exception => Status = $"Error: {exception.Message}");
    }

    /// <summary>Current status text.</summary>
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    /// <summary>Last snapshot timestamp.</summary>
    public string UpdatedAt
    {
        get => _updatedAt;
        private set => SetProperty(ref _updatedAt, value);
    }

    /// <summary>Command used by the sample page to request an update.</summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>Enables projection while the page is visible.</summary>
    public void Activate() => _subscription.Enable();

    /// <summary>Disables projection while the page is hidden.</summary>
    public void Deactivate() => _subscription.Disable();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeStarted, 1, 0) != 0)
        {
            await _disposeCompletion.Task;
            return;
        }

        try
        {
            Deactivate();
            await RefreshCommand.CancelAndWaitAsync();
            _subscription.Dispose();
            _disposeCompletion.TrySetResult(true);
        }
        catch (Exception exception)
        {
            _disposeCompletion.TrySetException(exception);
            throw;
        }
    }

    private void ProjectSnapshot()
    {
        var snapshot = _device.Snapshot;
        Status = $"Value {snapshot.Value} (update {snapshot.Sequence})";
        UpdatedAt = snapshot.UpdatedAt.ToLocalTime().ToString("G");
    }
}
