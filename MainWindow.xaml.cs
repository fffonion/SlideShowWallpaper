using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using SlideShowWallpaper.ViewModels;
using Windows.Graphics;
using WinRT.Interop;

namespace SlideShowWallpaper;

public sealed partial class MainWindow : Window
{
    private const string SettingsNavigationTag = "__settings";
    private static readonly TimeSpan CurrentImageCheckpointInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan PlaybackStatusRefreshInterval = TimeSpan.FromMinutes(1);
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
    private readonly TrayIconService _trayIconService;
    private readonly ImageOrderService _imageOrderService;
    private readonly Dictionary<string, ObservableCollection<ImagePreviewItem>> _previewItems = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBlock> _previewMetadataTexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> _previewLoadTokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherQueueTimer _currentImageCheckpointTimer;
    private readonly DispatcherQueueTimer _playbackStatusTimer;
    private readonly DispatcherQueueTimer _settingsApplyTimer;
    private readonly IntPtr _hwnd;
    private readonly bool _disableCloseToTray;
    private int _previewSessionVersion;
    private string? _selectedMonitorId;
    private bool _exitRequested;
    private bool _suppressPreviewSelection;
    private bool _isSettingsSelected;
    private bool _settingsUiUnloadedForBackground;

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

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(AppIconPaths.ResolveShellIconPath(Environment.ProcessPath, AppContext.BaseDirectory));
        ConfigureSettingsWindow();

        _hwnd = WindowNative.GetWindowHandle(this);
        Closed += MainWindow_Closed;
        LoadSettings();
        _trayIconService = new TrayIconService(
            _hwnd,
            () => _viewModel.Profiles,
            ShowSettingsWindow,
            ExitApplication,
            NextFromTray,
            TogglePauseFromTray,
            ToggleStopFromTray,
            HandleWindowMinimizedChanged);
        _coordinator.OrderedImagesChanged += Coordinator_OrderedImagesChanged;
        _coordinator.CurrentWallpaperChanged += Coordinator_CurrentWallpaperChanged;
        if (startInTray)
        {
            _settingsUiUnloadedForBackground = true;
        }
        else
        {
            RenderTabs();
        }

        ApplySettings();
        _currentImageCheckpointTimer.Start();
        _playbackStatusTimer.Start();
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
        _viewModel.PlaybackEnabled = true;
        ApplyTheme(_viewModel.ThemeMode);
    }

    private void RenderTabs(string? selectedMonitorId = null)
    {
        selectedMonitorId ??= SelectedProfile?.Id;
        MonitorContent.Content = null;
        MonitorNavigationPanel.Children.Clear();
        foreach (MonitorSettingsViewModel item in _viewModel.Monitors)
        {
            MonitorNavigationPanel.Children.Add(CreateMonitorNavigationItem(item.Profile));
        }

        SettingsNavigationPanel.Children.Clear();
        SettingsNavigationPanel.Children.Add(CreateSettingsNavigationItem());

        if (_isSettingsSelected)
        {
            _selectedMonitorId = null;
            UpdateMonitorNavigationVisuals();
            ShowSelectedMonitorPage();
            return;
        }

        if (MonitorNavigationPanel.Children.Count == 0)
        {
            _selectedMonitorId = null;
            _isSettingsSelected = true;
            UpdateMonitorNavigationVisuals();
            ShowSelectedMonitorPage();
            return;
        }

        _selectedMonitorId = _viewModel.Profiles.Any(profile => string.Equals(profile.Id, selectedMonitorId, StringComparison.OrdinalIgnoreCase))
            ? selectedMonitorId
            : _viewModel.Profiles.FirstOrDefault()?.Id;
        UpdateMonitorNavigationVisuals();
        ShowSelectedMonitorPage();
    }

    private Button CreateMonitorNavigationItem(MonitorProfile profile)
    {
        return CreateNavigationItem(profile.DisplayName, "\uE7F4", profile, MonitorNavigationItem_Click);
    }

    private Button CreateSettingsNavigationItem()
    {
        string label = LocalizedStrings.Get("Settings");
        return CreateNavigationItem(label, "\uE713", SettingsNavigationTag, SettingsNavigationItem_Click);
    }

    private Button CreateNavigationItem(string label, string glyph, object tag, RoutedEventHandler clickHandler)
    {
        var root = new Grid
        {
            ColumnSpacing = 0,
            Padding = new Thickness(0),
            MinHeight = 38,
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var indicator = new Border
        {
            Width = 3,
            Height = 22,
            CornerRadius = new CornerRadius(2),
            Background = GetThemeBrush("AccentFillColorDefaultBrush"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Opacity = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };
        root.Children.Add(indicator);

        var icon = new FontIcon
        {
            Glyph = glyph,
            FontSize = 17,
            Width = 24,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(icon, 2);
        root.Children.Add(icon);

        var text = new TextBlock
        {
            Text = label,
            FontSize = 14,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(text, label);
        Grid.SetColumn(text, 4);
        root.Children.Add(text);

        var surface = new Border
        {
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(0, 0, 10, 0),
            Background = GetThemeBrush("SubtleFillColorTransparentBrush"),
            Child = root,
        };

        var item = new Button
        {
            Content = surface,
            Tag = tag,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 38,
            Padding = new Thickness(0),
            Background = GetThemeBrush("SubtleFillColorTransparentBrush"),
            BorderThickness = new Thickness(0),
        };
        item.Click += clickHandler;
        surface.Tag = new MonitorNavigationVisuals(surface, indicator);
        AutomationProperties.SetName(item, label);
        AutomationProperties.SetAutomationId(item, tag is MonitorProfile profile ? $"MonitorNavigationItem_{profile.Id}" : "SettingsNavigationItem");
        return item;
    }

    private void UpdateMonitorNavigationVisuals()
    {
        foreach (UIElement element in MonitorNavigationPanel.Children)
        {
            if (element is not Button { Tag: MonitorProfile profile, Content: Border { Tag: MonitorNavigationVisuals visuals } } item)
            {
                continue;
            }

            bool isSelected = !_isSettingsSelected && string.Equals(profile.Id, _selectedMonitorId, StringComparison.OrdinalIgnoreCase);
            UpdateNavigationButtonVisual(item, visuals, isSelected);
        }

        foreach (UIElement element in SettingsNavigationPanel.Children)
        {
            if (element is Button { Tag: string tag, Content: Border { Tag: MonitorNavigationVisuals visuals } } item
                && string.Equals(tag, SettingsNavigationTag, StringComparison.Ordinal))
            {
                UpdateNavigationButtonVisual(item, visuals, _isSettingsSelected);
            }
        }
    }

    private static void UpdateNavigationButtonVisual(Button item, MonitorNavigationVisuals visuals, bool isSelected)
    {
        visuals.Surface.Background = GetThemeBrush(isSelected ? "SubtleFillColorSecondaryBrush" : "SubtleFillColorTransparentBrush");
        visuals.Indicator.Opacity = isSelected ? 1 : 0;
        item.Background = GetThemeBrush("SubtleFillColorTransparentBrush");
    }

    private void ShowSelectedMonitorPage()
    {
        if (_isSettingsSelected)
        {
            MonitorContent.Content = BuildAppSettingsPage();
            return;
        }

        MonitorContent.Content = SelectedProfile is { } profile ? BuildMonitorPage(profile) : null;
    }

    private UIElement BuildAppSettingsPage()
    {
        var root = new Grid
        {
            RowSpacing = 12,
            MaxWidth = 500,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var closeToTrayCheckBox = CreateCheckBox(_viewModel.CloseToTray, value => _viewModel.CloseToTray = value, LocalizedStrings.Get("AppSettingCloseToTray"));
        closeToTrayCheckBox.IsEnabled = !_disableCloseToTray;

        var form = new StackPanel
        {
            Spacing = 14,
        };
        form.Children.Add(CreateSettingsSection(
            LocalizedStrings.Get("Settings"),
            new SettingsRow(LocalizedStrings.Get("AppSettingAutostart"), CreateCheckBox(_viewModel.StartWithWindows, value =>
            {
                _viewModel.StartWithWindows = value;
                _autostartService.SetEnabled(value);
            }, LocalizedStrings.Get("AppSettingAutostart"))),
            new SettingsRow(LocalizedStrings.Get("AppSettingCloseToTray"), closeToTrayCheckBox),
            new SettingsRow(LocalizedStrings.Get("AppSettingTheme"), CreateChoiceCombo(ThemeModeChoices, _viewModel.ThemeMode, SetTheme, LocalizedStrings.Get("AppSettingTheme")))));

        Grid.SetRow(form, 0);
        root.Children.Add(form);
        return root;
    }

    private UIElement BuildMonitorPage(MonitorProfile profile)
    {
        var root = new Grid
        {
            ColumnSpacing = 16,
            RowSpacing = 8,
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        TextBlock metadata = CreatePreviewMetadataText(profile);
        Grid.SetRow(metadata, 0);
        Grid.SetColumn(metadata, 0);
        root.Children.Add(metadata);

        FrameworkElement commandBar = BuildMonitorCommandBar(profile);
        commandBar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(commandBar, 0);
        Grid.SetColumn(commandBar, 1);
        root.Children.Add(commandBar);

        FrameworkElement previewPane = BuildPreviewPane(profile, metadata);
        Grid.SetRow(previewPane, 1);
        Grid.SetColumn(previewPane, 0);
        root.Children.Add(previewPane);

        var scrollViewer = new ScrollViewer
        {
            Content = BuildMonitorSettings(profile),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetRow(scrollViewer, 1);
        Grid.SetColumn(scrollViewer, 1);
        root.Children.Add(scrollViewer);

        return root;
    }

    private TextBlock CreatePreviewMetadataText(MonitorProfile profile)
    {
        var metadata = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(profile.FolderPath) ? LocalizedStrings.Get("ImageCountZero") : FormatPreviewStatusText(profile),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _previewMetadataTexts[profile.Id] = metadata;
        AutomationProperties.SetName(metadata, LocalizedStrings.Format("MonitorImageCountAutomationFormat", profile.DisplayName));
        return metadata;
    }

    private FrameworkElement BuildPreviewPane(MonitorProfile profile, TextBlock metadata)
    {
        ObservableCollection<ImagePreviewItem> items = GetPreviewItems(profile);
        var previewHost = new Grid();
        var previewList = new ListView
        {
            ItemsSource = items,
            ItemTemplate = CreatePreviewTemplate(),
            SelectionMode = ListViewSelectionMode.Single,
            Tag = profile,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        AutomationProperties.SetName(previewList, LocalizedStrings.Format("MonitorPreviewsAutomationFormat", profile.DisplayName));
        previewList.SelectionChanged += PreviewList_SelectionChanged;
        previewHost.Children.Add(previewList);

        var loadingPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8,
            Visibility = string.IsNullOrWhiteSpace(profile.FolderPath) ? Visibility.Collapsed : Visibility.Visible,
        };
        var progressRing = new ProgressRing
        {
            IsActive = !string.IsNullOrWhiteSpace(profile.FolderPath),
            Width = 32,
            Height = 32,
        };
        AutomationProperties.SetName(progressRing, LocalizedStrings.Get("LoadingImagePreviews"));
        loadingPanel.Children.Add(progressRing);
        loadingPanel.Children.Add(new TextBlock
        {
            Text = LocalizedStrings.Get("LoadingImages"),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        previewHost.Children.Add(loadingPanel);

        StartPreviewLoad(profile, items, metadata, loadingPanel, progressRing);

        return previewHost;
    }

    private static DataTemplate CreatePreviewTemplate()
    {
        return ImagePreviewTemplateFactory.Create();
    }

    private ObservableCollection<ImagePreviewItem> GetPreviewItems(MonitorProfile profile)
    {
        if (_previewItems.TryGetValue(profile.Id, out ObservableCollection<ImagePreviewItem>? existing))
        {
            return existing;
        }

        var items = new ObservableCollection<ImagePreviewItem>();
        _previewItems[profile.Id] = items;
        return items;
    }

    private async void StartPreviewLoad(
        MonitorProfile profile,
        ObservableCollection<ImagePreviewItem> items,
        TextBlock metadataText,
        FrameworkElement loadingPanel,
        ProgressRing progressRing)
    {
        if (string.IsNullOrWhiteSpace(profile.FolderPath))
        {
            metadataText.Text = LocalizedStrings.Get("ImageCountZero");
            loadingPanel.Visibility = Visibility.Collapsed;
            progressRing.IsActive = false;
            return;
        }

        CancelPreviewLoad(profile.Id);
        var cancellation = new CancellationTokenSource();
        _previewLoadTokens[profile.Id] = cancellation;
        int previewSessionVersion = _previewSessionVersion;
        metadataText.Text = LocalizedStrings.Get("LoadingImages");
        loadingPanel.Visibility = Visibility.Visible;
        progressRing.IsActive = true;
        ImagePreviewItem[] reusableItems = [.. items];
        items.Clear();

        try
        {
            IReadOnlyList<ImageMetadata> images = await _imageOrderService.GetOrLoadOrderedImagesAsync(profile.FolderPath, profile.PlaybackOrder, profile.MediaFilter, cancellation.Token);
            if (IsPreviewLoadExpired(profile.Id, cancellation, previewSessionVersion))
            {
                return;
            }

            ImagePreviewCollectionUpdater.Apply(items, images, reusableItems);

            profile.TotalMediaCount = items.Count;
            UpdatePlaybackStatusText(profile);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (IsPreviewLoadExpired(profile.Id, cancellation, previewSessionVersion))
            {
                return;
            }

            AppLog.Write(exception);
            items.Clear();
            metadataText.Text = LocalizedStrings.Get("UnableToLoadImages");
        }
        finally
        {
            if (!IsPreviewLoadExpired(profile.Id, cancellation, previewSessionVersion))
            {
                _previewLoadTokens.Remove(profile.Id);
                loadingPanel.Visibility = Visibility.Collapsed;
                progressRing.IsActive = false;
            }

            cancellation.Dispose();
        }
    }

    private UIElement BuildMonitorSettings(MonitorProfile profile)
    {
        var root = new Grid
        {
            RowSpacing = 12,
            MaxWidth = 500,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var form = new StackPanel
        {
            Spacing = 14,
        };
        form.Children.Add(CreateSettingsSection(null, new SettingsRow(LocalizedStrings.Get("Folder"), CreateFolderControls(profile))));
        form.Children.Add(CreateSettingsSection(
            LocalizedStrings.Get("DisplaySettingsGroup"),
            new SettingsRow(LocalizedStrings.Get("Scale"), CreateChoiceCombo(ScaleModeChoices, profile.ScaleMode, value => profile.ScaleMode = value, LocalizedStrings.Get("ScaleModeAutomation"))),
            new SettingsRow(LocalizedStrings.Get("Offset"), CreateOffsetControls(profile))));
        form.Children.Add(CreateSettingsSection(
            LocalizedStrings.Get("MediaSettingsGroup"),
            new SettingsRow(LocalizedStrings.Get("MediaFilter"), CreateChoiceCombo(MediaFilterChoices, profile.MediaFilter, value =>
            {
                profile.MediaFilter = value;
                RenderTabs(profile.Id);
            }, LocalizedStrings.Get("MediaFilterAutomation"))),
            new SettingsRow(LocalizedStrings.Get("Order"), CreateChoiceCombo(PlaybackOrderChoices, profile.PlaybackOrder, value =>
            {
                profile.PlaybackOrder = value;
                if (value == PlaybackOrder.SingleLoop)
                {
                    profile.VideoLoop = true;
                }

                RenderTabs(profile.Id);
            }, LocalizedStrings.Get("PlaybackOrderAutomation"))),
            new SettingsRow(LocalizedStrings.Get("VideoLoop"), CreateCheckBox(profile.VideoLoop, value => profile.VideoLoop = value, LocalizedStrings.Get("VideoLoop"))),
            new SettingsRow(LocalizedStrings.Get("VideoSound"), CreateCheckBox(profile.VideoSoundEnabled, value => profile.VideoSoundEnabled = value, LocalizedStrings.Get("VideoSound"))),
            new SettingsRow(LocalizedStrings.Get("PauseVideoWhenOtherAppMaximized"), CreateCheckBox(profile.PauseVideoWhenOtherAppMaximized, value => profile.PauseVideoWhenOtherAppMaximized = value, LocalizedStrings.Get("PauseVideoWhenOtherAppMaximized")))));
        FrameworkElement intervalControl = CreateTimedNumberBox(
            ToDisplaySeconds(profile.IntervalSeconds, profile.IntervalUnit),
            profile.IntervalUnit,
            (value, unit) =>
            {
                profile.IntervalUnit = unit;
                profile.IntervalSeconds = Math.Max(5, (int)Math.Round(TimeUnitConverter.ToSeconds(value, unit)));
            },
            LocalizedStrings.Get("Interval"),
            profile.PlaybackOrder != PlaybackOrder.SingleLoop);

        form.Children.Add(CreateSettingsSection(
            LocalizedStrings.Get("PlaybackSettingsGroup"),
            new SettingsRow(LocalizedStrings.Get("Interval"), intervalControl),
            new SettingsRow(LocalizedStrings.Get("Transition"), CreateChoiceCombo(TransitionChoices, profile.Transition, value => profile.Transition = value, LocalizedStrings.Get("Transition"))),
            new SettingsRow(LocalizedStrings.Get("Duration"), CreateTimedNumberBox(
                ToDisplayDuration(profile.TransitionDurationMs, profile.TransitionDurationUnit),
                profile.TransitionDurationUnit,
                (value, unit) =>
                {
                    profile.TransitionDurationUnit = unit;
                    profile.TransitionDurationMs = Math.Max(0, TimeUnitConverter.ToMilliseconds(value, unit));
                },
                LocalizedStrings.Get("TransitionDurationAutomation")))));

        Grid.SetRow(form, 0);
        root.Children.Add(form);
        return root;
    }

    private FrameworkElement BuildMonitorCommandBar(MonitorProfile profile)
    {
        var stopButton = new AppBarToggleButton
        {
            IsChecked = profile.IsStopped,
        };
        UpdateStopButton(stopButton, profile.IsStopped);
        stopButton.Click += (_, _) =>
        {
            ToggleStop(profile, stopButton.IsChecked == true);
            UpdateStopButton(stopButton, profile.IsStopped);
        };

        var pauseButton = new AppBarToggleButton
        {
            IsChecked = profile.IsPaused,
        };
        UpdatePauseButton(pauseButton, profile.IsPaused);
        pauseButton.Click += (_, _) =>
        {
            TogglePause(profile, pauseButton.IsChecked == true);
            UpdatePauseButton(pauseButton, profile.IsPaused);
        };

        var nextButton = new AppBarButton
        {
            Icon = new SymbolIcon(Symbol.Forward),
            Label = LocalizedStrings.Get("Next"),
        };
        nextButton.Click += async (_, _) =>
        {
            await ShowNextAsync(profile);
            stopButton.IsChecked = profile.IsStopped;
            UpdateStopButton(stopButton, profile.IsStopped);
        };

        var shuffleButton = new AppBarButton
        {
            Icon = new FontIcon { Glyph = "\uE8B1" },
            Label = LocalizedStrings.Get("Shuffle"),
            IsEnabled = profile.PlaybackOrder == PlaybackOrder.Random,
        };
        shuffleButton.Click += (_, _) => ShuffleProfile(profile);

        var commandBar = new CommandBar
        {
            DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        commandBar.PrimaryCommands.Add(stopButton);
        commandBar.PrimaryCommands.Add(pauseButton);
        commandBar.PrimaryCommands.Add(nextButton);
        commandBar.PrimaryCommands.Add(shuffleButton);
        return commandBar;
    }

    private void ConfigureSettingsWindow()
    {
        const int preferredWidth = 1460;
        const int preferredHeight = 1340;
        DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;
        int width = Math.Min(preferredWidth, workArea.Width);
        int height = Math.Min(preferredHeight, workArea.Height);
        AppWindow.Resize(new SizeInt32(width, height));
        AppWindow.Move(new PointInt32(
            workArea.X + Math.Max(0, (workArea.Width - width) / 2),
            workArea.Y + Math.Max(0, (workArea.Height - height) / 2)));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
        }
    }

    private Grid CreateFolderControls(MonitorProfile profile)
    {
        var panel = new Grid
        {
            ColumnSpacing = 10,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var pathText = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(profile.FolderPath) ? LocalizedStrings.Get("FolderNone") : profile.FolderPath,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(pathText, 0);
        panel.Children.Add(pathText);

        var folderButton = new Button
        {
            Content = new SymbolIcon(Symbol.Folder),
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
        };
        AutomationProperties.SetName(folderButton, LocalizedStrings.Get("Folder"));
        ToolTipService.SetToolTip(folderButton, LocalizedStrings.Get("Folder"));
        folderButton.Click += async (_, _) => await OpenFolderAsync(profile);
        Grid.SetColumn(folderButton, 1);
        panel.Children.Add(folderButton);

        return panel;
    }

    private static Border CreateSettingsSection(string? title, params SettingsRow[] rows)
    {
        var stack = new StackPanel();
        if (!string.IsNullOrEmpty(title))
        {
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 22,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(16, 14, 16, 10),
            };
            AutomationProperties.SetName(titleBlock, title);
            stack.Children.Add(titleBlock);
            stack.Children.Add(CreateSettingsDivider());
        }

        for (int i = 0; i < rows.Length; i++)
        {
            if (i > 0)
            {
                stack.Children.Add(CreateSettingsDivider());
            }

            stack.Children.Add(CreateSettingsRow(rows[i]));
        }

        return new Border
        {
            Background = GetThemeBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = stack,
        };
    }

    private static Border CreateSettingsRow(SettingsRow row)
    {
        var content = new Grid
        {
            ColumnSpacing = 12,
            MinHeight = 50,
            Padding = new Thickness(16, 6, 16, 6),
        };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var text = new TextBlock
        {
            Text = row.Label,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxLines = 3,
        };
        AutomationProperties.SetName(text, row.Label);
        Grid.SetColumn(text, 0);
        content.Children.Add(text);

        row.Control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(row.Control, 1);
        content.Children.Add(row.Control);

        return new Border
        {
            Child = content,
        };
    }

    private static Border CreateSettingsDivider()
    {
        return new Border
        {
            Height = 1,
            Background = GetThemeBrush("DividerStrokeColorDefaultBrush"),
        };
    }

    private static Brush GetThemeBrush(string resourceKey)
    {
        return Application.Current.Resources.TryGetValue(resourceKey, out object value) && value is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private StackPanel CreateOffsetControls(MonitorProfile profile)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
        };
        panel.Children.Add(CreateLabeledNumberBox("X", profile.OffsetX, value => profile.OffsetX = value));
        panel.Children.Add(CreateLabeledNumberBox("Y", profile.OffsetY, value => profile.OffsetY = value));
        return panel;
    }

    private StackPanel CreateLabeledNumberBox(string label, double value, Action<double> changed)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
        };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
        });
        panel.Children.Add(CreateNumberBox(value, changed, label));
        return panel;
    }

    private ComboBox CreateChoiceCombo<T>(IReadOnlyList<Choice<T>> choices, T selected, Action<T> changed, string automationName)
        where T : notnull
    {
        Choice<T> selectedChoice = FindChoice(choices, selected);
        var combo = new ComboBox
        {
            ItemsSource = choices,
            SelectedItem = selectedChoice,
            Width = 270,
        };
        AutomationProperties.SetName(combo, automationName);
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is Choice<T> choice)
            {
                changed(choice.Value);
                ScheduleApplySettings();
            }
        };
        return combo;
    }

    private NumberBox CreateNumberBox(double value, Action<double> changed, string automationName)
    {
        var numberBox = new NumberBox
        {
            Value = value,
            SmallChange = 1,
            LargeChange = 10,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 96,
        };
        AutomationProperties.SetName(numberBox, automationName);
        numberBox.ValueChanged += (_, args) =>
        {
            if (!double.IsNaN(args.NewValue))
            {
                changed(args.NewValue);
                ScheduleApplySettings();
            }
        };
        return numberBox;
    }

    private StackPanel CreateTimedNumberBox(double value, TimeUnit unit, Action<double, TimeUnit> changed, string automationName, bool isEnabled = true)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        TimeUnit selectedUnit = unit;
        var valueBox = CreateNumberBox(value, newValue =>
        {
            changed(newValue, selectedUnit);
        }, automationName);
        var unitCombo = CreateChoiceCombo(TimeUnitChoices, unit, newUnit =>
        {
            selectedUnit = newUnit;
            double currentValue = double.IsNaN(valueBox.Value) ? value : valueBox.Value;
            changed(currentValue, newUnit);
        }, LocalizedStrings.Format("TimeUnitAutomationFormat", automationName));
        unitCombo.Width = 128;
        valueBox.IsEnabled = isEnabled;
        unitCombo.IsEnabled = isEnabled;

        panel.Children.Add(valueBox);
        panel.Children.Add(unitCombo);
        return panel;
    }

    private CheckBox CreateCheckBox(bool isChecked, Action<bool> changed, string automationName)
    {
        var checkBox = new CheckBox
        {
            IsChecked = isChecked,
        };
        AutomationProperties.SetName(checkBox, automationName);
        checkBox.Checked += (_, _) =>
        {
            changed(true);
            ScheduleApplySettings();
        };
        checkBox.Unchecked += (_, _) =>
        {
            changed(false);
            ScheduleApplySettings();
        };
        return checkBox;
    }

    private async Task OpenFolderAsync(MonitorProfile profile)
    {
        string? folder = await _folderPickerService.PickFolderAsync(_hwnd);
        if (folder is null)
        {
            return;
        }

        profile.FolderPath = folder;
        profile.SelectedImagePath = string.Empty;
        RenderTabs(profile.Id);
        ApplySettings();
    }

    private void MonitorNavigationItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MonitorProfile profile })
        {
            CancelSelectedPreviewLoad();
            _selectedMonitorId = profile.Id;
            _isSettingsSelected = false;
        }

        UpdateMonitorNavigationVisuals();
        ShowSelectedMonitorPage();
    }

    private void SettingsNavigationItem_Click(object sender, RoutedEventArgs e)
    {
        CancelSelectedPreviewLoad();
        _selectedMonitorId = null;
        _isSettingsSelected = true;
        UpdateMonitorNavigationVisuals();
        ShowSelectedMonitorPage();
    }

    private void Coordinator_OrderedImagesChanged(object? sender, OrderedImagesChangedEventArgs args)
    {
        if (!_previewItems.TryGetValue(args.MonitorId, out ObservableCollection<ImagePreviewItem>? items))
        {
            return;
        }

        ImagePreviewItem[] reusableItems = [.. items];
        ImagePreviewCollectionUpdater.Apply(items, args.Images, reusableItems);
        MonitorProfile? profile = _viewModel.Profiles.FirstOrDefault(item => string.Equals(item.Id, args.MonitorId, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
        {
            profile.TotalMediaCount = items.Count;
            UpdatePlaybackStatusText(profile);
        }
    }

    private void Coordinator_CurrentWallpaperChanged(object? sender, CurrentWallpaperChangedEventArgs args)
    {
        _ = CurrentWallpaperSelectionUpdater.Update(_viewModel.Profiles, args.MonitorId, args.ImagePath);
        MonitorProfile? profile = _viewModel.Profiles.FirstOrDefault(item => string.Equals(item.Id, args.MonitorId, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        profile.CurrentMediaIndex = args.CurrentIndex;
        profile.TotalMediaCount = args.TotalCount;
        profile.CurrentMediaStartedAt = DateTimeOffset.Now;
        UpdatePlaybackStatusText(profile);
    }

    private async void PreviewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressPreviewSelection)
        {
            return;
        }

        if (sender is not ListView { Tag: MonitorProfile profile, SelectedItem: ImagePreviewItem item })
        {
            return;
        }

        profile.SelectedImagePath = item.Path;
        ApplySettings();
        try
        {
            IReadOnlyList<string> orderedPaths = sender is ListView { ItemsSource: IEnumerable<ImagePreviewItem> previewItems }
                ? previewItems.Select(previewItem => previewItem.Path).ToArray()
                : [];
            await _coordinator.ShowImageAsync(profile, item.Path, orderedPaths);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private void TogglePauseFromTray(string monitorId)
    {
        MonitorProfile? profile = _viewModel.Profiles.FirstOrDefault(item => string.Equals(item.Id, monitorId, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        TogglePause(profile, !profile.IsPaused);
        RenderTabs(profile.Id);
    }

    private void ToggleStopFromTray(string monitorId)
    {
        MonitorProfile? profile = _viewModel.Profiles.FirstOrDefault(item => string.Equals(item.Id, monitorId, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        ToggleStop(profile, !profile.IsStopped);
        RenderTabs(profile.Id);
    }

    private void NextFromTray(string monitorId)
    {
        _ = _coordinator.ShowNextAsync(monitorId);
    }

    private void TogglePause(MonitorProfile profile, bool isPaused)
    {
        profile.IsPaused = isPaused;
        _coordinator.PauseOrResume(profile.Id, profile.IsPaused);
        ApplySettings();
    }

    private void ToggleStop(MonitorProfile profile, bool isStopped)
    {
        profile.IsStopped = isStopped;
        ApplySettings();
    }

    private async Task ShowNextAsync(MonitorProfile profile)
    {
        if (profile.IsStopped)
        {
            profile.IsStopped = false;
            ApplySettings();
        }

        await _coordinator.ShowNextAsync(profile.Id);
    }

    private void ShuffleProfile(MonitorProfile profile)
    {
        if (profile.PlaybackOrder != PlaybackOrder.Random)
        {
            return;
        }

        if (_previewItems.TryGetValue(profile.Id, out ObservableCollection<ImagePreviewItem>? items) && items.Count > 1)
        {
            IReadOnlyList<ImageMetadata> shuffled = ImageLibrary.SortImages(
                items.Select(item => item.Metadata),
                PlaybackOrder.Random);
            _suppressPreviewSelection = true;
            try
            {
                ImagePreviewCollectionUpdater.Apply(items, shuffled);
            }
            finally
            {
                _suppressPreviewSelection = false;
            }
        }

        _coordinator.Shuffle(profile.Id);
    }

    private void ApplySettings()
    {
        _settingsApplyTimer.Stop();
        WallpaperConfig config = CreateConfig();
        _settingsStore.Save(config);
        _coordinator.ApplyProfiles(config.Monitors, config.PlaybackEnabled);
    }

    private void ScheduleApplySettings()
    {
        _settingsApplyTimer.Stop();
        _settingsApplyTimer.Start();
    }

    private WallpaperConfig CreateConfig()
    {
        WallpaperConfig existingConfig = _settingsStore.Load();
        return new WallpaperConfig
        {
            StartWithWindows = _viewModel.StartWithWindows,
            CloseToTray = _disableCloseToTray ? existingConfig.CloseToTray : _viewModel.CloseToTray,
            ThemeMode = _viewModel.ThemeMode,
            PlaybackEnabled = true,
            Monitors = _viewModel.Profiles.ToList(),
        };
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _settingsApplyTimer.Stop();
        if (_exitRequested || !_viewModel.CloseToTray)
        {
            _currentImageCheckpointTimer.Stop();
            _playbackStatusTimer.Stop();
            UnloadPreviewState();
            ShutdownApplication();
            return;
        }

        args.Handled = true;
        UnloadSettingsUiForTray();
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
    }

    public void ShowSettingsWindow()
    {
        EnsureSettingsUiLoaded();

        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_RESTORE);
        Activate();
        NativeMethods.SetForegroundWindow(_hwnd);
    }

    private void HandleWindowMinimizedChanged(bool isMinimized)
    {
        if (_exitRequested)
        {
            return;
        }

        if (isMinimized)
        {
            UnloadSettingsUiForBackground();
        }
        else
        {
            EnsureSettingsUiLoaded();
        }
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private void ShutdownApplication()
    {
        _coordinator.OrderedImagesChanged -= Coordinator_OrderedImagesChanged;
        _coordinator.CurrentWallpaperChanged -= Coordinator_CurrentWallpaperChanged;
        _trayIconService.Dispose();
        _coordinator.Shutdown();
    }

    private void SaveCurrentImageCheckpoint()
    {
        _settingsStore.Save(CreateConfig());
    }

    private void SetTheme(AppThemeMode themeMode)
    {
        if (_viewModel.ThemeMode == themeMode)
        {
            return;
        }

        _viewModel.ThemeMode = themeMode;
        ApplyTheme(themeMode);
        ApplySettings();
    }

    private void ApplyTheme(AppThemeMode themeMode)
    {
        Root.RequestedTheme = themeMode switch
        {
            AppThemeMode.Light => ElementTheme.Light,
            AppThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private static Choice<T> FindChoice<T>(IReadOnlyList<Choice<T>> choices, T selected)
        where T : notnull
    {
        return choices.FirstOrDefault(choice => EqualityComparer<T>.Default.Equals(choice.Value, selected)) ?? choices[0];
    }

    private void CancelPreviewLoad(string monitorId)
    {
        if (_previewLoadTokens.Remove(monitorId, out CancellationTokenSource? cancellation))
        {
            cancellation.Cancel();
        }
    }

    private void CancelSelectedPreviewLoad()
    {
        if (!string.IsNullOrWhiteSpace(_selectedMonitorId))
        {
            CancelPreviewLoad(_selectedMonitorId);
        }
    }

    private void UnloadSettingsUiForTray()
    {
        UnloadSettingsUiForBackground();
    }

    private void UnloadSettingsUiForBackground()
    {
        if (_settingsUiUnloadedForBackground)
        {
            return;
        }

        _previewSessionVersion++;
        foreach (string monitorId in _previewLoadTokens.Keys.ToArray())
        {
            CancelPreviewLoad(monitorId);
        }

        MonitorNavigationPanel.Children.Clear();
        SettingsNavigationPanel.Children.Clear();
        MonitorContent.Content = null;
        UnloadPreviewState();
        _settingsUiUnloadedForBackground = true;
        ProcessMemoryTrimmer.TrimCurrentProcess();
    }

    private void EnsureSettingsUiLoaded()
    {
        if (!_settingsUiUnloadedForBackground && MonitorNavigationPanel.Children.Count > 0)
        {
            return;
        }

        _settingsUiUnloadedForBackground = false;
        RenderTabs(_selectedMonitorId);
    }

    private bool IsPreviewLoadExpired(string monitorId, CancellationTokenSource cancellation, int previewSessionVersion)
    {
        return cancellation.IsCancellationRequested
            || previewSessionVersion != _previewSessionVersion
            || !_previewLoadTokens.TryGetValue(monitorId, out CancellationTokenSource? current)
            || !ReferenceEquals(current, cancellation);
    }

    private void UnloadPreviewState()
    {
        foreach (ObservableCollection<ImagePreviewItem> items in _previewItems.Values)
        {
            ImagePreviewCollectionUpdater.Clear(items);
        }

        _previewItems.Clear();
        _previewMetadataTexts.Clear();
    }

    private void UpdateAllPlaybackStatusTexts()
    {
        foreach (MonitorProfile profile in _viewModel.Profiles)
        {
            UpdatePlaybackStatusText(profile);
        }
    }

    private void UpdatePlaybackStatusText(MonitorProfile profile)
    {
        if (_previewMetadataTexts.TryGetValue(profile.Id, out TextBlock? metadataText))
        {
            metadataText.Text = FormatPreviewStatusText(profile);
        }
    }

    private static string FormatPreviewStatusText(MonitorProfile profile)
    {
        if (profile.PlaybackOrder == PlaybackOrder.SingleLoop)
        {
            return PlaybackStatusFormatter.FormatPreviewStatusWithoutRemaining(profile.CurrentMediaIndex, profile.TotalMediaCount);
        }

        int remainingSeconds = PlaybackStatusFormatter.CalculateLoopRemainingSeconds(
            profile.CurrentMediaIndex,
            profile.TotalMediaCount,
            profile.IntervalSeconds,
            profile.CurrentMediaStartedAt,
            DateTimeOffset.Now,
            profile.PlaybackOrder);
        return PlaybackStatusFormatter.FormatPreviewStatus(profile.CurrentMediaIndex, profile.TotalMediaCount, remainingSeconds);
    }

    private static double ToDisplaySeconds(int seconds, TimeUnit unit)
    {
        return unit switch
        {
            TimeUnit.Hours => seconds / 3600.0,
            TimeUnit.Minutes => seconds / 60.0,
            _ => seconds,
        };
    }

    private static double ToDisplayDuration(int milliseconds, TimeUnit unit)
    {
        double seconds = milliseconds / 1000.0;
        return unit switch
        {
            TimeUnit.Hours => seconds / 3600.0,
            TimeUnit.Minutes => seconds / 60.0,
            _ => seconds,
        };
    }

    private static void UpdateStopButton(AppBarToggleButton button, bool isStopped)
    {
        button.Icon = new SymbolIcon(isStopped ? Symbol.Play : Symbol.Stop);
        button.Label = isStopped ? LocalizedStrings.Get("Start") : LocalizedStrings.Get("Stop");
        AutomationProperties.SetName(button, button.Label);
    }

    private static void UpdatePauseButton(AppBarToggleButton button, bool isPaused)
    {
        button.Icon = new SymbolIcon(isPaused ? Symbol.Play : Symbol.Pause);
        button.Label = isPaused ? LocalizedStrings.Get("Resume") : LocalizedStrings.Get("Pause");
        AutomationProperties.SetName(button, button.Label);
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
