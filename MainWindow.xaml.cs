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
    private static readonly TimeSpan SettingsApplyDelay = TimeSpan.FromMilliseconds(200);

    private static IReadOnlyList<Choice<PlaybackOrder>> PlaybackOrderChoices =>
    [
        new(PlaybackOrder.Random, LocalizedStrings.Get("PlaybackRandom")),
        new(PlaybackOrder.NameAsc, LocalizedStrings.Get("PlaybackNameAsc")),
        new(PlaybackOrder.NameDesc, LocalizedStrings.Get("PlaybackNameDesc")),
        new(PlaybackOrder.ModifiedDateAsc, LocalizedStrings.Get("PlaybackModifiedDateAsc")),
        new(PlaybackOrder.ModifiedDateDesc, LocalizedStrings.Get("PlaybackModifiedDateDesc")),
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
    private readonly DispatcherQueueTimer _settingsApplyTimer;
    private readonly IntPtr _hwnd;
    private bool _exitRequested;

    public MainWindow(
        MonitorService monitorService,
        WallpaperPlaybackCoordinator coordinator,
        SettingsStore settingsStore,
        AutostartService autostartService,
        FolderPickerService folderPickerService,
        ImageOrderService imageOrderService)
    {
        _monitorService = monitorService;
        _coordinator = coordinator;
        _settingsStore = settingsStore;
        _autostartService = autostartService;
        _folderPickerService = folderPickerService;
        _imageOrderService = imageOrderService;

        InitializeComponent();
        Title = LocalizedStrings.Get("AppTitle");
        AppTitleBar.Title = LocalizedStrings.Get("AppTitle");
        ThemeComboBox.ItemsSource = ThemeModeChoices;
        _settingsApplyTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _settingsApplyTimer.Interval = SettingsApplyDelay;
        _settingsApplyTimer.IsRepeating = false;
        _settingsApplyTimer.Tick += (_, _) => ApplySettings();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(AppIconPaths.ResolveShellIconPath(Environment.ProcessPath, AppContext.BaseDirectory));
        ConfigureSettingsWindow();

        _hwnd = WindowNative.GetWindowHandle(this);
        Closed += MainWindow_Closed;
        LoadSettings();
        _trayIconService = new TrayIconService(_hwnd, () => _viewModel.Profiles, ShowSettingsWindow, ExitApplication, NextFromTray, TogglePauseFromTray, ToggleStopFromTray);
        _coordinator.OrderedImagesChanged += Coordinator_OrderedImagesChanged;
        RenderTabs();
        ApplySettings();
    }

    private MonitorProfile? SelectedProfile => MonitorTabs.SelectedItem is TabViewItem { Tag: MonitorProfile profile }
        ? profile
        : _viewModel.Profiles.FirstOrDefault();

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
        _viewModel.CloseToTray = config.CloseToTray;
        _viewModel.ThemeMode = config.ThemeMode;
        _viewModel.PlaybackEnabled = true;
        AutostartButton.IsChecked = _viewModel.StartWithWindows;
        CloseToTrayButton.IsChecked = _viewModel.CloseToTray;
        ThemeComboBox.SelectedItem = FindChoice(ThemeModeChoices, _viewModel.ThemeMode);
        ApplyTheme(_viewModel.ThemeMode);
    }

    private void RenderTabs(string? selectedMonitorId = null)
    {
        selectedMonitorId ??= SelectedProfile?.Id;
        MonitorTabs.TabItems.Clear();
        foreach (MonitorSettingsViewModel item in _viewModel.Monitors)
        {
            MonitorTabs.TabItems.Add(new TabViewItem
            {
                Header = item.Profile.DisplayName,
                IsClosable = false,
                Tag = item.Profile,
                Content = BuildMonitorPage(item.Profile),
            });
        }

        if (MonitorTabs.TabItems.Count == 0)
        {
            return;
        }

        int selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(selectedMonitorId))
        {
            for (int i = 0; i < MonitorTabs.TabItems.Count; i++)
            {
                if (MonitorTabs.TabItems[i] is TabViewItem { Tag: MonitorProfile profile }
                    && string.Equals(profile.Id, selectedMonitorId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        MonitorTabs.SelectedIndex = selectedIndex;
    }

    private UIElement BuildMonitorPage(MonitorProfile profile)
    {
        var root = new Grid
        {
            ColumnSpacing = 16,
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        FrameworkElement previewPane = BuildPreviewPane(profile);
        Grid.SetColumn(previewPane, 0);
        root.Children.Add(previewPane);

        var scrollViewer = new ScrollViewer
        {
            Content = BuildMonitorSettings(profile),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetColumn(scrollViewer, 1);
        root.Children.Add(scrollViewer);

        return root;
    }

    private FrameworkElement BuildPreviewPane(MonitorProfile profile)
    {
        ObservableCollection<ImagePreviewItem> items = GetPreviewItems(profile);
        var root = new Grid
        {
            RowSpacing = 8,
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var metadata = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(profile.FolderPath) ? LocalizedStrings.Get("ImageCountZero") : LocalizedStrings.Get("LoadingImages"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        _previewMetadataTexts[profile.Id] = metadata;
        AutomationProperties.SetName(metadata, LocalizedStrings.Format("MonitorImageCountAutomationFormat", profile.DisplayName));
        root.Children.Add(metadata);

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

        Grid.SetRow(previewHost, 1);
        root.Children.Add(previewHost);
        StartPreviewLoad(profile, items, metadata, loadingPanel, progressRing);

        return root;
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
        metadataText.Text = LocalizedStrings.Get("LoadingImages");
        loadingPanel.Visibility = Visibility.Visible;
        progressRing.IsActive = true;
        ImagePreviewItem[] reusableItems = [.. items];
        items.Clear();

        try
        {
            IReadOnlyList<ImageMetadata> images = await _imageOrderService.ReloadOrderedImagesAsync(profile.FolderPath, profile.PlaybackOrder, cancellation.Token);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            ImagePreviewCollectionUpdater.Apply(items, images, reusableItems);

            metadataText.Text = LocalizedStrings.Format("ImageCountFormat", items.Count);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            items.Clear();
            metadataText.Text = LocalizedStrings.Get("UnableToLoadImages");
        }
        finally
        {
            if (_previewLoadTokens.TryGetValue(profile.Id, out CancellationTokenSource? current) && ReferenceEquals(current, cancellation))
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
            RowSpacing = 14,
            MaxWidth = 680,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        FrameworkElement commandBar = BuildMonitorCommandBar(profile);
        Grid.SetRow(commandBar, 0);
        root.Children.Add(commandBar);

        var form = new Grid
        {
            RowSpacing = 12,
            ColumnSpacing = 12,
        };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        int row = 0;
        AddRow(form, row++, LocalizedStrings.Get("Folder"), new TextBlock { Text = string.IsNullOrWhiteSpace(profile.FolderPath) ? LocalizedStrings.Get("FolderNone") : profile.FolderPath, TextTrimming = TextTrimming.CharacterEllipsis });
        AddRow(form, row++, LocalizedStrings.Get("Scale"), CreateChoiceCombo(ScaleModeChoices, profile.ScaleMode, value => profile.ScaleMode = value, LocalizedStrings.Get("ScaleModeAutomation")));
        AddRow(form, row++, LocalizedStrings.Get("Order"), CreateChoiceCombo(PlaybackOrderChoices, profile.PlaybackOrder, value =>
        {
            profile.PlaybackOrder = value;
            RenderTabs(profile.Id);
        }, LocalizedStrings.Get("PlaybackOrderAutomation")));
        AddRow(form, row++, LocalizedStrings.Get("Offset"), CreateOffsetControls(profile));
        AddRow(form, row++, LocalizedStrings.Get("Interval"), CreateTimedNumberBox(
            ToDisplaySeconds(profile.IntervalSeconds, profile.IntervalUnit),
            profile.IntervalUnit,
            (value, unit) =>
            {
                profile.IntervalUnit = unit;
                profile.IntervalSeconds = Math.Max(5, (int)Math.Round(TimeUnitConverter.ToSeconds(value, unit)));
            },
            LocalizedStrings.Get("Interval")));
        AddRow(form, row++, LocalizedStrings.Get("Transition"), CreateChoiceCombo(TransitionChoices, profile.Transition, value => profile.Transition = value, LocalizedStrings.Get("Transition")));
        AddRow(form, row++, LocalizedStrings.Get("Duration"), CreateTimedNumberBox(
            ToDisplayDuration(profile.TransitionDurationMs, profile.TransitionDurationUnit),
            profile.TransitionDurationUnit,
            (value, unit) =>
            {
                profile.TransitionDurationUnit = unit;
                profile.TransitionDurationMs = Math.Max(0, TimeUnitConverter.ToMilliseconds(value, unit));
            },
            LocalizedStrings.Get("TransitionDurationAutomation")));

        Grid.SetRow(form, 1);
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

        var folderButton = new AppBarButton
        {
            Icon = new SymbolIcon(Symbol.OpenFile),
            Label = LocalizedStrings.Get("Folder"),
        };
        folderButton.Click += async (_, _) => await OpenFolderAsync(profile);

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
        commandBar.PrimaryCommands.Add(folderButton);
        commandBar.PrimaryCommands.Add(stopButton);
        commandBar.PrimaryCommands.Add(pauseButton);
        commandBar.PrimaryCommands.Add(nextButton);
        commandBar.PrimaryCommands.Add(shuffleButton);
        return commandBar;
    }

    private void ConfigureSettingsWindow()
    {
        const int width = 1180;
        const int preferredHeight = 900;
        DisplayArea displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        RectInt32 workArea = displayArea.WorkArea;
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

    private static void AddRow(Grid root, int row, string label, FrameworkElement control)
    {
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(text, label);
        Grid.SetRow(text, row);
        Grid.SetColumn(text, 0);
        root.Children.Add(text);

        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(control, row);
        Grid.SetColumn(control, 1);
        root.Children.Add(control);
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
            MinWidth = 180,
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

    private StackPanel CreateTimedNumberBox(double value, TimeUnit unit, Action<double, TimeUnit> changed, string automationName)
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
        unitCombo.MinWidth = 112;

        panel.Children.Add(valueBox);
        panel.Children.Add(unitCombo);
        return panel;
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

    private void RefreshScreens_Click(object sender, RoutedEventArgs e)
    {
        WallpaperConfig existing = CreateConfig();
        _viewModel.Monitors.Clear();
        foreach (MonitorProfile current in _monitorService.GetCurrentMonitors())
        {
            MonitorProfile profile = existing.Monitors.FirstOrDefault(saved => string.Equals(saved.Id, current.Id, StringComparison.OrdinalIgnoreCase)) ?? current;
            profile.DisplayName = current.DisplayName;
            _viewModel.Monitors.Add(new MonitorSettingsViewModel(profile));
        }

        RenderTabs();
        ApplySettings();
    }

    private void AutostartButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.StartWithWindows = AutostartButton.IsChecked == true;
        _autostartService.SetEnabled(_viewModel.StartWithWindows);
        ApplySettings();
    }

    private void CloseToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CloseToTray = CloseToTrayButton.IsChecked == true;
        ApplySettings();
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is Choice<AppThemeMode> choice)
        {
            SetTheme(choice.Value);
        }
    }

    private void MonitorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private void Coordinator_OrderedImagesChanged(object? sender, OrderedImagesChangedEventArgs args)
    {
        if (!_previewItems.TryGetValue(args.MonitorId, out ObservableCollection<ImagePreviewItem>? items))
        {
            return;
        }

        ImagePreviewItem[] reusableItems = [.. items];
        ImagePreviewCollectionUpdater.Apply(items, args.Images, reusableItems);
        if (_previewMetadataTexts.TryGetValue(args.MonitorId, out TextBlock? metadataText))
        {
            metadataText.Text = LocalizedStrings.Format("ImageCountFormat", items.Count);
        }
    }

    private async void PreviewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView { Tag: MonitorProfile profile, SelectedItem: ImagePreviewItem item })
        {
            return;
        }

        profile.SelectedImagePath = item.Path;
        ApplySettings();
        IReadOnlyList<string> orderedPaths = sender is ListView { ItemsSource: IEnumerable<ImagePreviewItem> previewItems }
            ? previewItems.Select(previewItem => previewItem.Path).ToArray()
            : [];
        await _coordinator.ShowImageAsync(profile, item.Path, orderedPaths);
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
            ImagePreviewCollectionUpdater.Apply(items, shuffled);
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
        return new WallpaperConfig
        {
            StartWithWindows = _viewModel.StartWithWindows,
            CloseToTray = _viewModel.CloseToTray,
            ThemeMode = _viewModel.ThemeMode,
            PlaybackEnabled = true,
            Monitors = _viewModel.Profiles.ToList(),
        };
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _settingsApplyTimer.Stop();
        ClearPreviewThumbnails();
        if (_exitRequested || !_viewModel.CloseToTray)
        {
            ShutdownApplication();
            return;
        }

        args.Handled = true;
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
    }

    private void ShowSettingsWindow()
    {
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
        Activate();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private void ShutdownApplication()
    {
        _coordinator.OrderedImagesChanged -= Coordinator_OrderedImagesChanged;
        _trayIconService.Dispose();
        _coordinator.Shutdown();
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

    private void ClearPreviewThumbnails()
    {
        foreach (ImagePreviewItem item in _previewItems.Values.SelectMany(items => items))
        {
            item.ClearThumbnail();
        }
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

    private sealed record Choice<T>(T Value, string Label)
    {
        public override string ToString()
        {
            return Label;
        }
    }
}
