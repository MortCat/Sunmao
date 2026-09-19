using System.ComponentModel;
using System.Windows;
using Sunmao.Core.Logging;
using Sunmao.Wpf.Polling;

namespace Sunmao.App;

/// <summary>Application shell containing the generated sample page.</summary>
public partial class MainWindow : Window
{
    private readonly DeviceViewModel _viewModel;
    private readonly Func<Task> _requestApplicationShutdown;
    private readonly ILogSink _log;
    private Task? _closeTask;
    private bool _allowClose;

    /// <summary>Creates the shell and binds the Component-backed page.</summary>
    /// <param name="requestApplicationShutdown">Awaits application-owned cleanup before exit.</param>
    /// <param name="logSink">Receives close failures.</param>
    public MainWindow(
        DeviceComponent device,
        IUiPollingSource pulse,
        Func<Task> requestApplicationShutdown,
        ILogSink? logSink = null)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(pulse);
        ArgumentNullException.ThrowIfNull(requestApplicationShutdown);
        InitializeComponent();
        _viewModel = new DeviceViewModel(device, pulse);
        _requestApplicationShutdown = requestApplicationShutdown;
        _log = logSink ?? Sunmao.Core.Logging.NullLogSink.Instance;
        DataContext = _viewModel;
        Loaded += (_, _) => _viewModel.Activate();
    }

    /// <summary>Defers the close until the view model and application resources are drained.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (_allowClose)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        _closeTask ??= CloseAfterDrainAsync();
    }

    private async Task CloseAfterDrainAsync()
    {
        try
        {
            await _viewModel.DisposeAsync();
        }
        catch (Exception exception)
        {
            _log.Write(LogLevel.Error, "application", "Window cleanup failed.", exception);
        }

        _allowClose = true;
        try
        {
            await _requestApplicationShutdown();
        }
        catch (Exception exception)
        {
            _log.Write(LogLevel.Error, "application", "Application shutdown failed.", exception);
        }

        await Dispatcher.InvokeAsync(Close);
    }
}
