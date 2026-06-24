using Microsoft.UI.Xaml;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper;

public partial class App : Application
{
    private Window? _window;
    private readonly MonitorService _monitorService = new();
    private readonly DesktopHostService _desktopHostService = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly AutostartService _autostartService = new();
    private readonly FolderPickerService _folderPickerService = new();
    private readonly ImageOrderService _imageOrderService = new();
    private readonly FolderChangeWatcherService _folderChangeWatcherService = new();
    private readonly HardwareMonitorService _hardwareMonitorService = new();
    private WallpaperPlaybackCoordinator? _coordinator;
    private SingleInstanceService? _singleInstanceService;

    public App()
    {
        AppLanguageService.ApplyStartupLanguageOverride(_settingsStore);
        InitializeComponent();
        UnhandledException += (_, args) =>
        {
            AppLog.Write(args.Exception);
            args.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                AppLog.Write(exception);
            }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            AppLog.Write("Launch start");
            AppTempPaths.Cleanup();
            LaunchOptions launchOptions = LaunchOptions.FromArguments(Environment.GetCommandLineArgs().Skip(1));
            if (!launchOptions.AllowMultipleInstances)
            {
                _singleInstanceService = new SingleInstanceService();
                if (!_singleInstanceService.TryAcquirePrimary())
                {
                    _ = _singleInstanceService.NotifyPrimaryAsync().GetAwaiter().GetResult();
                    _singleInstanceService.Dispose();
                    _singleInstanceService = null;
                    Exit();
                    Environment.Exit(0);
                    return;
                }
            }

            _coordinator = new WallpaperPlaybackCoordinator(_monitorService, _desktopHostService, _imageOrderService, _folderChangeWatcherService, _hardwareMonitorService);
            _window = new MainWindow(
                _monitorService,
                _coordinator,
                _settingsStore,
                _autostartService,
                _folderPickerService,
                _imageOrderService,
                _hardwareMonitorService,
                launchOptions.DisableCloseToTray,
                launchOptions.StartInTray);
            if (!launchOptions.StartInTray)
            {
                _window.Activate();
            }

            _singleInstanceService?.StartActivationListener(ShowExistingInstanceWindow);
            AppLog.Write("Launch activated");
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            throw;
        }
    }

    private void ShowExistingInstanceWindow()
    {
        if (_window is not MainWindow mainWindow)
        {
            return;
        }

        mainWindow.DispatcherQueue.TryEnqueue(mainWindow.ShowSettingsWindow);
    }
}
