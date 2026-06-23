using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using SlideShowWallpaper.ViewModels;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using WinRT.Interop;

namespace SlideShowWallpaper;

public sealed partial class MainWindow : Window
{
    private const string SettingsNavigationTag = "__settings";
    private const double DefaultPreviewPaneWidth = 250;
    private const double MinimumPreviewPaneWidth = 160;
    private const double MaximumPreviewPaneWidth = 520;
    private const double PreviewPopupWidth = 420;
    private const double PreviewPopupHeight = 260;
    private const double PreviewPopupPortraitWidth = 300;
    private const double PreviewPopupPortraitHeight = 420;
    private const double PreviewPopupPadding = 8;
    private const double PreviewPopupBorderThickness = 1;
    private const double PreviewPopupGap = 8;
    private static readonly TimeSpan CurrentImageCheckpointInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan PlaybackStatusRefreshInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan BackgroundStartupTrimDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BackgroundWallpaperReadyTrimDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SettingsApplyDelay = TimeSpan.FromMilliseconds(200);

    private static IReadOnlyList<Choice<PlaybackOrder>> PlaybackOrderChoices =>
    [
        new(PlaybackOrder.Random, LocalizedStrings.Get("PlaybackRandom")),
        new(PlaybackOrder.SingleLoop, LocalizedStrings.Get("PlaybackSingleLoop")),
        new(PlaybackOrder.NameAsc, LocalizedStrings.Get("PlaybackNameAsc")),
        new(PlaybackOrder.NameDesc, LocalizedStrings.Get("PlaybackNameDesc")),
        new(PlaybackOrder.ModifiedDateAsc, LocalizedStrings.Get("PlaybackModifiedDateAsc")),
        new(PlaybackOrder.ModifiedDateDesc, LocalizedStrings.Get("PlaybackModifiedDateDesc")),
    ];

    private static IReadOnlyList<Choice<PlaybackMediaFilter>> MediaFilterChoices =>
    [
        new(PlaybackMediaFilter.ImagesOnly, LocalizedStrings.Get("MediaFilterImages")),
        new(PlaybackMediaFilter.VideosOnly, LocalizedStrings.Get("MediaFilterVideos")),
        new(PlaybackMediaFilter.ImagesAndVideos, LocalizedStrings.Get("MediaFilterImagesAndVideos")),
    ];

    private static IReadOnlyList<Choice<TimeUnit>> TimeUnitChoices =>
    [
        new(TimeUnit.Seconds, LocalizedStrings.Get("TimeSeconds")),
        new(TimeUnit.Minutes, LocalizedStrings.Get("TimeMinutes")),
        new(TimeUnit.Hours, LocalizedStrings.Get("TimeHours")),
    ];

    private static IReadOnlyList<Choice<AppThemeMode>> ThemeModeChoices =>
    [
        new(AppThemeMode.System, LocalizedStrings.Get("ThemeSystem")),
        new(AppThemeMode.Light, LocalizedStrings.Get("ThemeLight")),
        new(AppThemeMode.Dark, LocalizedStrings.Get("ThemeDark")),
    ];

    private static IReadOnlyList<Choice<AppLanguageMode>> LanguageModeChoices =>
    [
        new(AppLanguageMode.System, LocalizedStrings.Get("LanguageSystem")),
        new(AppLanguageMode.English, LocalizedStrings.Get("LanguageEnglish")),
        new(AppLanguageMode.SimplifiedChinese, LocalizedStrings.Get("LanguageSimplifiedChinese")),
        new(AppLanguageMode.TraditionalChinese, LocalizedStrings.Get("LanguageTraditionalChinese")),
        new(AppLanguageMode.Japanese, LocalizedStrings.Get("LanguageJapanese")),
    ];

    private static IReadOnlyList<Choice<WallpaperScaleMode>> ScaleModeChoices =>
    [
        new(WallpaperScaleMode.Fit, LocalizedStrings.Get("ScaleFit")),
        new(WallpaperScaleMode.Cover, LocalizedStrings.Get("ScaleCover")),
        new(WallpaperScaleMode.Stretch, LocalizedStrings.Get("ScaleStretch")),
        new(WallpaperScaleMode.Original, LocalizedStrings.Get("ScaleOriginal")),
    ];

    private static IReadOnlyList<Choice<WallpaperTransition>> TransitionChoices =>
    [
        new(WallpaperTransition.None, LocalizedStrings.Get("TransitionNone")),
        new(WallpaperTransition.Fade, LocalizedStrings.Get("TransitionFade")),
        new(WallpaperTransition.Slide, LocalizedStrings.Get("TransitionSlide")),
    ];

    private readonly MainViewModel _viewModel = new();
    private readonly MonitorService _monitorService;
    private readonly WallpaperPlaybackCoordinator _coordinator;
    private readonly SettingsStore _settingsStore;
    private readonly AutostartService _autostartService;
    private readonly FolderPickerService _folderPickerService;
    private readonly ThumbnailCacheService _thumbnailCacheService = new();
    private readonly TrayIconService _trayIconService;
    private readonly ImageOrderService _imageOrderService;
    private readonly Dictionary<string, ObservableCollection<ImagePreviewItem>> _previewItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBlock> _previewMetadataTexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _previewLoadTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherQueueTimer _currentImageCheckpointTimer;
    private readonly DispatcherQueueTimer _playbackStatusTimer;
    private readonly DispatcherQueueTimer _settingsApplyTimer;
    private readonly DispatcherQueueTimer _previewPopupTimer;
    private readonly object _backgroundMemoryTrimLock = new();
    private readonly IntPtr _hwnd;
    private readonly bool _disableCloseToTray;
    private TextBlock? _thumbnailCacheSizeText;
    private ProgressRing? _thumbnailCacheSizeProgress;
    private Button? _clearThumbnailCacheButton;
    private CancellationTokenSource? _thumbnailCacheSizeCancellation;
    private CancellationTokenSource? _backgroundMemoryTrimCancellation;
    private Popup? _previewPopup;
    private Border? _previewPopupSurface;
    private Microsoft.UI.Xaml.Controls.Image? _previewPopupImage;
    private Border? _previewPopupVideoFrame;
    private MediaPlayerElement? _previewPopupVideo;
    private MediaPlayer? _previewPopupPlayer;
    private CancellationTokenSource? _previewPopupCancellation;
    private ImagePreviewItem? _previewPopupPendingItem;
    private ListViewItem? _previewPopupPendingContainer;
    private MonitorProfile? _previewPopupPendingProfile;
    private int _previewSessionVersion;
    private string? _selectedMonitorId;
    private bool _exitRequested;
    private bool _suppressPreviewSelection;
    private bool _isSettingsSelected;
    private bool _settingsUiUnloadedForBackground;
    private bool _backgroundStartupTrimPending;
    private bool _contentHeightAdjusted;
    private double _previewPopupCurrentWidth = PreviewPopupWidth;
    private double _previewPopupCurrentHeight = PreviewPopupHeight;
    private int _thumbnailCacheSizeLoadVersion;

    private sealed record MonitorNavigationVisuals(Border Surface, Border Indicator);

    public MainWindow(
        MonitorService monitorService,
        WallpaperPlaybackCoordinator coordinator,
        SettingsStore settingsStore,
        AutostartService autostartService,
        FolderPickerService folderPickerService,
        ImageOrderService imageOrderService,
        bool disableCloseToTray = false,
        bool startInTray = false)
    {
        _monitorService = monitorService;
        _coordinator = coordinator;
        _settingsStore = settingsStore;
        _autostartService = autostartService;
        _folderPickerService = folderPickerService;
        _imageOrderService = imageOrderService;
        _disableCloseToTray = disableCloseToTray;

        InitializeComponent();
        Title = LocalizedStrings.Get("AppTitle");
        AppTitleBar.Title = LocalizedStrings.Get("AppTitle");
        _settingsApplyTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _settingsApplyTimer.Interval = SettingsApplyDelay;
        _settingsApplyTimer.IsRepeating = false;
        _settingsApplyTimer.Tick += (_, _) => ApplySettings();
        _currentImageCheckpointTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _currentImageCheckpointTimer.Interval = CurrentImageCheckpointInterval;
        _currentImageCheckpointTimer.IsRepeating = true;
        _currentImageCheckpointTimer.Tick += (_, _) => SaveCurrentImageCheckpoint();
        _playbackStatusTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _playbackStatusTimer.Interval = PlaybackStatusRefreshInterval;
        _playbackStatusTimer.IsRepeating = true;
        _playbackStatusTimer.Tick += (_, _) => UpdateAllPlaybackStatusTexts();
        _previewPopupTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _previewPopupTimer.Interval = PreviewPopupPolicy.GetHoverDelay(_viewModel.PreviewPopupDelaySeconds);
        _previewPopupTimer.IsRepeating = false;
        _previewPopupTimer.Tick += PreviewPopupTimer_Tick;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(AppIconPaths.ResolveShellIconPath(Environment.ProcessPath, AppContext.BaseDirectory));
        _hwnd = WindowNative.GetWindowHandle(this);
        Closed += MainWindow_Closed;
        LoadSettings();
        UpdatePreviewPopupDelay();
        _trayIconService = new TrayIconService(
            _hwnd,
            () => _viewModel.Profiles,
            ShowSettingsWindow,
            ExitApplication,
            NextFromTray,
            TogglePauseFromTray,
            ToggleStopFromTray,
            HandleWindowMinimizedChanged,
            HandleDisplayPowerPauseChanged);
        _coordinator.OrderedImagesChanged += Coordinator_OrderedImagesChanged;
        _coordinator.CurrentWallpaperChanged += Coordinator_CurrentWallpaperChanged;
        Root.Loaded += Root_Loaded;
        if (startInTray)
        {
            _settingsUiUnloadedForBackground = true;
        }
        else
        {
            RenderTabs();
        }

        ConfigureSettingsWindow();
        ApplySettings();
        _currentImageCheckpointTimer.Start();
        _playbackStatusTimer.Start();
        if (startInTray)
        {
            _backgroundStartupTrimPending = true;
            ScheduleBackgroundMemoryTrim(BackgroundStartupTrimDelay);
        }
    }

    private void HandleDisplayPowerPauseChanged(bool isPaused)
    {
        _coordinator.SetDisplayPowerVideoPause(isPaused);
    }

    private MonitorProfile? SelectedProfile => _viewModel.Profiles.FirstOrDefault(profile => string.Equals(profile.Id, _selectedMonitorId, StringComparison.OrdinalIgnoreCase))
        ?? _viewModel.Profiles.FirstOrDefault();

    private void LoadSettings()
    {
        WallpaperConfig config = _settingsStore.Load();
        IReadOnlyList<MonitorProfile> currentMonitors = _monitorService.GetCurrentMonitors();
        foreach (MonitorProfile current in currentMonitors)
        {
            MonitorProfile profile = config.Monitors.FirstOrDefault(saved => string.Equals(saved.Id, current.Id, StringComparison.OrdinalIgnoreCase)) ?? current;
            profile.DisplayName = current.DisplayName;
            if (!config.PlaybackEnabled)
            {
                profile.IsStopped = true;
            }

            if (profile.PlaybackOrder == PlaybackOrder.SequentialLoop)
            {
                profile.PlaybackOrder = PlaybackOrder.Random;
            }

            _viewModel.Monitors.Add(new MonitorSettingsViewModel(profile));
        }

        _viewModel.StartWithWindows = _autostartService.IsEnabled();
        _viewModel.CloseToTray = _disableCloseToTray ? false : config.CloseToTray;
        _viewModel.ThemeMode = config.ThemeMode;
        _viewModel.LanguageMode = config.LanguageMode;
        _viewModel.PlaybackEnabled = true;
        _viewModel.AutoTrackNewFiles = config.AutoTrackNewFiles;
        _viewModel.GlobalMute = config.GlobalMute;
        _viewModel.ThumbnailCacheEnabled = config.ThumbnailCacheEnabled;
        _viewModel.PauseVideoWhenDisplayOffOrSleeping = config.PauseVideoWhenDisplayOffOrSleeping;
        _viewModel.PreviewPopupDelaySeconds = Math.Max(PreviewPopupPolicy.MinimumHoverDelaySeconds, config.PreviewPopupDelaySeconds);
        ApplyTheme(_viewModel.ThemeMode);
    }

    private readonly record struct SettingsRow(string Label, FrameworkElement Control);

    private sealed record Choice<T>(T Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }
}
