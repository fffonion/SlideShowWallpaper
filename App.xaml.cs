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
    private WallpaperPlaybackCoordinator? _coordinator;

    public App()
    {
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
            LaunchOptions launchOptions = LaunchOptions.FromArguments(Environment.GetCommandLineArgs().Skip(1));
            _coordinator = new WallpaperPlaybackCoordinator(_monitorService, _desktopHostService, _imageOrderService);
            _window = new MainWindow(_monitorService, _coordinator, _settingsStore, _autostartService, _folderPickerService, _imageOrderService);
            if (!launchOptions.StartInTray)
            {
                _window.Activate();
            }

            AppLog.Write("Launch activated");
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            throw;
        }
    }
}
