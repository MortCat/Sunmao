using System.IO;
using System.Windows;
using Sunmao.Core.Lifecycle;
using Sunmao.Diagnostics;
using Sunmao.Wpf.Polling;
using Sunmao.Wpf.Threading;

namespace Sunmao.App;

/// <summary>Owns application composition and the shutdown sequence.</summary>
public partial class App : Application
{
    private AsyncTextLogSink? _log;
    private ComponentLifecycleCoordinator? _lifecycle;
    private DeviceComponent? _device;
    private UiPollingTimer? _pulse;
    private Task? _startupTask;
    private Task? _shutdownTask;

    /// <summary>Initializes the dispatcher and owns the asynchronous startup operation.</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        UiDispatcher.Initialize(Dispatcher);
        _log = new AsyncTextLogSink();
        _log.TryConfigure(new LogFileDestination(
            Path.Combine(AppContext.BaseDirectory, "logs"),
            "sunmao-app"));
        _device = new DeviceComponent(_log);
        _lifecycle = new ComponentLifecycleCoordinator([_device]);
        _startupTask = StartApplicationAsync();
    }

    /// <summary>Starts the lifecycle before showing the shell.</summary>
    private async Task StartApplicationAsync()
    {
        var log = _log ?? throw new InvalidOperationException("The diagnostic sink was not created.");
        var lifecycle = _lifecycle ?? throw new InvalidOperationException("The lifecycle was not created.");
        var device = _device ?? throw new InvalidOperationException("The device Component was not created.");

        try
        {
            await lifecycle.InitializeAsync(CancellationToken.None);
            await lifecycle.StartAsync(CancellationToken.None);
            _pulse = new UiPollingTimer(hertz: 10, log);
            MainWindow = new MainWindow(device, _pulse, RequestShutdownAsync, log);
            MainWindow.Show();
        }
        catch (Exception exception)
        {
            log.Write(Sunmao.Core.Logging.LogLevel.Error, "application", "Startup failed.", exception);
            await RequestShutdownAsync();
        }
    }

    /// <summary>Begins the single application shutdown operation.</summary>
    private Task RequestShutdownAsync() => _shutdownTask ??= ShutdownApplicationAsync();

    /// <summary>Drains owned resources before allowing WPF to exit.</summary>
    private async Task ShutdownApplicationAsync()
    {
        try
        {
            _pulse?.Dispose();
            _pulse = null;
        }
        catch (Exception exception)
        {
            _log?.Write(Sunmao.Core.Logging.LogLevel.Error, "application", "UI shutdown failed.", exception);
        }

        try
        {
            if (_lifecycle is not null)
            {
                await _lifecycle.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            _log?.Write(Sunmao.Core.Logging.LogLevel.Error, "application", "Shutdown failed.", exception);
        }
        finally
        {
            try
            {
                if (_log is not null)
                {
                    await _log.DisposeAsync();
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Diagnostic shutdown failed: {exception}");
            }

            Shutdown();
        }
    }

    /// <summary>Completes the WPF exit callback after owned shutdown has finished.</summary>
    protected override void OnExit(ExitEventArgs e) => base.OnExit(e);
}
