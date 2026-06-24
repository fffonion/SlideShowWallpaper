using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper;

public sealed partial class MainWindow
{
    private UIElement BuildAppSettingsPage()
    {
        var root = new Grid
        {
            RowSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var closeToTrayCheckBox = CreateCheckBox(_viewModel.CloseToTray, value => _viewModel.CloseToTray = value, LocalizedStrings.Get("AppSettingCloseToTray"));
        closeToTrayCheckBox.IsEnabled = !_disableCloseToTray;
        FrameworkElement thumbnailCacheControls = CreateThumbnailCacheControls();

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
            new SettingsRow(LocalizedStrings.Get("AppSettingTheme"), CreateChoiceCombo(ThemeModeChoices, _viewModel.ThemeMode, SetTheme, LocalizedStrings.Get("AppSettingTheme"))),
            new SettingsRow(LocalizedStrings.Get("AppSettingLanguage"), CreateChoiceCombo(LanguageModeChoices, _viewModel.LanguageMode, SetLanguage, LocalizedStrings.Get("AppSettingLanguage"))),
            new SettingsRow(LocalizedStrings.Get("AppSettingAutoTrackNewFiles"), CreateCheckBox(_viewModel.AutoTrackNewFiles, value => _viewModel.AutoTrackNewFiles = value, LocalizedStrings.Get("AppSettingAutoTrackNewFiles"))),
            new SettingsRow(LocalizedStrings.Get("AppSettingGlobalMute"), CreateCheckBox(_viewModel.GlobalMute, value => _viewModel.GlobalMute = value, LocalizedStrings.Get("AppSettingGlobalMute"))),
            new SettingsRow(LocalizedStrings.Get("AppSettingThumbnailCache"), thumbnailCacheControls),
            new SettingsRow(LocalizedStrings.Get("AppSettingPauseVideoWhenDisplayOffOrSleeping"), CreateCheckBox(
                _viewModel.PauseVideoWhenDisplayOffOrSleeping,
                value => _viewModel.PauseVideoWhenDisplayOffOrSleeping = value,
                LocalizedStrings.Get("AppSettingPauseVideoWhenDisplayOffOrSleeping"))),
            new SettingsRow(LocalizedStrings.Get("AppSettingVideoPreviewDelay"), CreateNumberBox(
                _viewModel.PreviewPopupDelaySeconds,
                value =>
                {
                    _viewModel.PreviewPopupDelaySeconds = Math.Max(PreviewPopupPolicy.MinimumHoverDelaySeconds, (int)Math.Round(value));
                    UpdatePreviewPopupDelay();
                },
                LocalizedStrings.Get("AppSettingVideoPreviewDelay")))));
        form.Children.Add(CreateHardwareMonitorSettingsSection());

        Grid.SetRow(form, 0);
        root.Children.Add(form);
        StartThumbnailCacheSizeLoad();
        return root;
    }

    private Border CreateHardwareMonitorSettingsSection()
    {
        HardwareMonitorConfig config = _viewModel.HardwareMonitor;
        RefreshHardwareSnapshot();
        EnsureDefaultHardwareSensors(config);

        TextBlock previewText = CreateHardwareOverlayPreviewText(config);
        TextBox templateBox = CreateHardwareTemplateTextBox(config, previewText);
        FrameworkElement sensorList = CreateHardwareSensorSelectionList(config, previewText);
        FrameworkElement buttonRow = CreateHardwareTemplateButtons(config);
        return CreateSettingsSection(
            LocalizedStrings.Get("HardwareMonitorSettingsGroup"),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorEnabled"), CreateCheckBox(config.IsEnabled, value => config.IsEnabled = value, LocalizedStrings.Get("HardwareMonitorEnabled"))),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorTargetDisplay"), CreateChoiceCombo(CreateHardwareMonitorTargetChoices(), config.TargetMonitorId, value => config.TargetMonitorId = value, LocalizedStrings.Get("HardwareMonitorTargetDisplay"))),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorSensors"), sensorList),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorPosition"), CreateHardwarePositionControls(config)),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorStyle"), CreateHardwareStyleControls(config)),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorTemplate"), templateBox),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorPreview"), CreateHardwarePreviewSurface(config, previewText)),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorTemplateActions"), buttonRow));
    }

    private IReadOnlyList<Choice<string>> CreateHardwareMonitorTargetChoices()
    {
        var choices = new List<Choice<string>>
        {
            new(string.Empty, LocalizedStrings.Get("HardwareMonitorAllDisplays")),
        };
        choices.AddRange(_viewModel.Profiles.Select(profile => new Choice<string>(profile.Id, profile.DisplayName)));
        return choices;
    }

    private TextBox CreateHardwareTemplateTextBox(HardwareMonitorConfig config, TextBlock previewText)
    {
        var textBox = new TextBox
        {
            Text = config.TemplateText,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 96,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(textBox, LocalizedStrings.Get("HardwareMonitorTemplate"));
        textBox.TextChanged += (_, _) =>
        {
            config.TemplateText = textBox.Text;
            UpdateHardwareOverlayPreview(previewText, config);
            ScheduleApplySettings();
        };
        return textBox;
    }

    private FrameworkElement CreateHardwareSensorSelectionList(HardwareMonitorConfig config, TextBlock previewText)
    {
        var stack = new StackPanel
        {
            Spacing = 4,
        };
        IReadOnlyList<HardwareSensorReading> sensors = _hardwareMonitorSnapshot?.Sensors ?? [];
        if (sensors.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = LocalizedStrings.Get("HardwareMonitorNoSensors"),
                TextWrapping = TextWrapping.Wrap,
            });
        }
        else
        {
            foreach (HardwareSensorReading sensor in sensors.OrderBy(sensor => sensor.Group).ThenBy(sensor => sensor.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                var checkBox = new CheckBox
                {
                    Content = $"{GetHardwareMetricGroupLabel(sensor.Group)} · {sensor.DisplayName}",
                    IsChecked = config.SelectedSensorIds.Contains(sensor.Id, StringComparer.OrdinalIgnoreCase),
                };
                checkBox.Content = $"{GetHardwareMetricGroupLabel(sensor.Group)} - {sensor.DisplayName}";
                AutomationProperties.SetName(checkBox, sensor.DisplayName);
                checkBox.Checked += (_, _) =>
                {
                    if (!config.SelectedSensorIds.Contains(sensor.Id, StringComparer.OrdinalIgnoreCase))
                    {
                        config.SelectedSensorIds.Add(sensor.Id);
                    }

                    UpdateHardwareOverlayPreview(previewText, config);
                    ScheduleApplySettings();
                };
                checkBox.Unchecked += (_, _) =>
                {
                    config.SelectedSensorIds.RemoveAll(id => string.Equals(id, sensor.Id, StringComparison.OrdinalIgnoreCase));
                    UpdateHardwareOverlayPreview(previewText, config);
                    ScheduleApplySettings();
                };
                stack.Children.Add(checkBox);
            }
        }

        return new ScrollViewer
        {
            Content = stack,
            MaxHeight = 260,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private FrameworkElement CreateHardwarePositionControls(HardwareMonitorConfig config)
    {
        var panel = new Grid
        {
            ColumnSpacing = 10,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        FrameworkElement xControl = CreateLabeledNumberBox("X", config.X, value => config.X = value);
        FrameworkElement yControl = CreateLabeledNumberBox("Y", config.Y, value => config.Y = value);
        Grid.SetColumn(xControl, 0);
        Grid.SetColumn(yControl, 1);
        panel.Children.Add(xControl);
        panel.Children.Add(yControl);
        return panel;
    }

    private FrameworkElement CreateHardwareStyleControls(HardwareMonitorConfig config)
    {
        var panel = new Grid
        {
            ColumnSpacing = 10,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        FrameworkElement fontControl = CreateLabeledNumberBox(LocalizedStrings.Get("HardwareMonitorFontSizeShort"), config.FontSize, value => config.FontSize = Math.Max(10, value));
        FrameworkElement opacityControl = CreateLabeledNumberBox(LocalizedStrings.Get("HardwareMonitorOpacityShort"), config.Opacity, value => config.Opacity = Math.Clamp(value, 0.1, 1));
        Grid.SetColumn(fontControl, 0);
        Grid.SetColumn(opacityControl, 1);
        panel.Children.Add(fontControl);
        panel.Children.Add(opacityControl);
        return panel;
    }

    private TextBlock CreateHardwareOverlayPreviewText(HardwareMonitorConfig config)
    {
        var text = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            TextWrapping = TextWrapping.Wrap,
        };
        UpdateHardwareOverlayPreview(text, config);
        return text;
    }

    private FrameworkElement CreateHardwarePreviewSurface(HardwareMonitorConfig config, TextBlock previewText)
    {
        var overlay = new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(170, 0, 0, 0)),
            BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = previewText,
        };
        var canvas = new Canvas
        {
            MinHeight = 150,
        };
        Canvas.SetLeft(overlay, Math.Max(0, config.X));
        Canvas.SetTop(overlay, Math.Max(0, config.Y));

        bool isDragging = false;
        double dragStartX = 0;
        double dragStartY = 0;
        double startLeft = 0;
        double startTop = 0;
        overlay.PointerPressed += (_, args) =>
        {
            isDragging = true;
            dragStartX = args.GetCurrentPoint(canvas).Position.X;
            dragStartY = args.GetCurrentPoint(canvas).Position.Y;
            startLeft = Canvas.GetLeft(overlay);
            startTop = Canvas.GetTop(overlay);
            overlay.CapturePointer(args.Pointer);
            args.Handled = true;
        };
        overlay.PointerMoved += (_, args) =>
        {
            if (!isDragging)
            {
                return;
            }

            global::Windows.Foundation.Point point = args.GetCurrentPoint(canvas).Position;
            double left = Math.Max(0, startLeft + point.X - dragStartX);
            double top = Math.Max(0, startTop + point.Y - dragStartY);
            Canvas.SetLeft(overlay, left);
            Canvas.SetTop(overlay, top);
            config.X = left;
            config.Y = top;
            ScheduleApplySettings();
            args.Handled = true;
        };
        overlay.PointerReleased += (_, args) =>
        {
            isDragging = false;
            overlay.ReleasePointerCapture(args.Pointer);
            args.Handled = true;
        };
        overlay.PointerCanceled += (_, args) =>
        {
            isDragging = false;
            overlay.ReleasePointerCapture(args.Pointer);
        };
        overlay.PointerCaptureLost += (_, _) => isDragging = false;
        canvas.Children.Add(overlay);

        return new Border
        {
            MinHeight = 150,
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(220, 20, 20, 20)),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = canvas,
        };
    }

    private FrameworkElement CreateHardwareTemplateButtons(HardwareMonitorConfig config)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var refreshButton = new Button
        {
            Content = new SymbolIcon(Symbol.Refresh),
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
        };
        AutomationProperties.SetName(refreshButton, LocalizedStrings.Get("RefreshHardwareSensors"));
        ToolTipService.SetToolTip(refreshButton, LocalizedStrings.Get("RefreshHardwareSensors"));
        refreshButton.Click += (_, _) =>
        {
            RefreshHardwareSnapshot();
            RenderTabs(_selectedMonitorId);
        };

        var importButton = new Button
        {
            Content = new SymbolIcon(Symbol.OpenFile),
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
        };
        AutomationProperties.SetName(importButton, LocalizedStrings.Get("ImportHardwareTemplate"));
        ToolTipService.SetToolTip(importButton, LocalizedStrings.Get("ImportHardwareTemplate"));
        importButton.Click += async (_, _) => await ImportHardwareTemplateAsync(config);

        var exportButton = new Button
        {
            Content = new SymbolIcon(Symbol.Save),
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
        };
        AutomationProperties.SetName(exportButton, LocalizedStrings.Get("ExportHardwareTemplate"));
        ToolTipService.SetToolTip(exportButton, LocalizedStrings.Get("ExportHardwareTemplate"));
        exportButton.Click += async (_, _) => await ExportHardwareTemplateAsync(config);

        panel.Children.Add(refreshButton);
        panel.Children.Add(importButton);
        panel.Children.Add(exportButton);
        return panel;
    }

    private void RefreshHardwareSnapshot()
    {
        try
        {
            _hardwareMonitorSnapshot = _hardwareMonitorService.GetSnapshot();
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            _hardwareMonitorSnapshot = new HardwareMonitorSnapshot([], DateTimeOffset.Now);
        }
    }

    private void EnsureDefaultHardwareSensors(HardwareMonitorConfig config)
    {
        if (config.SelectedSensorIds.Count > 0 || _hardwareMonitorSnapshot is not { Sensors.Count: > 0 } snapshot)
        {
            return;
        }

        config.SelectedSensorIds = snapshot.Sensors
            .Where(sensor => sensor.Kind is HardwareMetricKind.Temperature or HardwareMetricKind.FanRpm or HardwareMetricKind.MemoryAvailable or HardwareMetricKind.VramAvailable or HardwareMetricKind.Power)
            .Take(8)
            .Select(sensor => sensor.Id)
            .ToList();
    }

    private void UpdateHardwareOverlayPreview(TextBlock previewText, HardwareMonitorConfig config)
    {
        HardwareMonitorSnapshot snapshot = _hardwareMonitorSnapshot ?? new HardwareMonitorSnapshot([], DateTimeOffset.Now);
        previewText.Text = HardwareOverlayTextRenderer.Render(config, snapshot);
        previewText.FontSize = Math.Max(10, config.FontSize);
        previewText.Opacity = Math.Clamp(config.Opacity, 0.1, 1);
    }

    private async Task ImportHardwareTemplateAsync(HardwareMonitorConfig config)
    {
        string? path = await _folderPickerService.PickOpenFileAsync(_hwnd, ".json");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            HardwareOverlayTemplate template = await HardwareOverlayTemplateService.ImportAsync(path);
            HardwareOverlayTemplateService.ApplyToConfig(template, config);
            ApplySettings();
            RenderTabs(_selectedMonitorId);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private async Task ExportHardwareTemplateAsync(HardwareMonitorConfig config)
    {
        string? path = await _folderPickerService.PickSaveFileAsync(_hwnd, ".json", "hardware-overlay-template");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await HardwareOverlayTemplateService.ExportAsync(HardwareOverlayTemplateService.FromConfig(config), path);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private static string GetHardwareMetricGroupLabel(HardwareMetricGroup group)
    {
        return group switch
        {
            HardwareMetricGroup.Cpu => LocalizedStrings.Get("HardwareMetricGroupCpu"),
            HardwareMetricGroup.Gpu => LocalizedStrings.Get("HardwareMetricGroupGpu"),
            HardwareMetricGroup.Storage => LocalizedStrings.Get("HardwareMetricGroupStorage"),
            HardwareMetricGroup.Memory => LocalizedStrings.Get("HardwareMetricGroupMemory"),
            HardwareMetricGroup.Motherboard => LocalizedStrings.Get("HardwareMetricGroupMotherboard"),
            _ => LocalizedStrings.Get("HardwareMetricGroupOther"),
        };
    }

    private UIElement BuildMonitorPage(MonitorProfile profile)
    {
        var root = new Grid
        {
            ColumnSpacing = 0,
            RowSpacing = 8,
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var previewColumn = new ColumnDefinition
        {
            Width = new GridLength(DefaultPreviewPaneWidth),
            MinWidth = MinimumPreviewPaneWidth,
            MaxWidth = MaximumPreviewPaneWidth,
        };
        root.ColumnDefinitions.Add(previewColumn);
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        TextBlock metadata = CreatePreviewMetadataText(profile);
        Grid.SetRow(metadata, 0);
        Grid.SetColumn(metadata, 0);
        root.Children.Add(metadata);

        FrameworkElement commandBar = BuildMonitorCommandBar(profile);
        commandBar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(commandBar, 0);
        Grid.SetColumn(commandBar, 2);
        root.Children.Add(commandBar);

        FrameworkElement previewPane = BuildPreviewPane(profile, metadata);
        previewPane.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetRow(previewPane, 1);
        Grid.SetColumn(previewPane, 0);
        root.Children.Add(previewPane);

        FrameworkElement resizeHandle = CreatePreviewResizeHandle(root, previewColumn);
        Grid.SetRow(resizeHandle, 1);
        Grid.SetColumn(resizeHandle, 1);
        root.Children.Add(resizeHandle);

        var scrollViewer = new ScrollViewer
        {
            Content = BuildMonitorSettings(profile),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetRow(scrollViewer, 1);
        Grid.SetColumn(scrollViewer, 2);
        root.Children.Add(scrollViewer);

        return root;
    }

    private static FrameworkElement CreatePreviewResizeHandle(Grid root, ColumnDefinition previewColumn)
    {
        var line = new Border
        {
            Width = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = GetThemeBrush("DividerStrokeColorDefaultBrush"),
            Opacity = 0.65,
        };
        var handle = new Border
        {
            Width = 16,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Child = line,
        };
        AutomationProperties.SetName(handle, LocalizedStrings.Get("PreviewResizeHandle"));

        bool isDragging = false;
        double startX = 0;
        double startWidth = 0;

        handle.PointerEntered += (_, _) => line.Opacity = 1;
        handle.PointerExited += (_, _) =>
        {
            if (!isDragging)
            {
                line.Opacity = 0.65;
            }
        };
        handle.PointerPressed += (_, args) =>
        {
            isDragging = true;
            startX = args.GetCurrentPoint(root).Position.X;
            startWidth = previewColumn.ActualWidth;
            handle.CapturePointer(args.Pointer);
            line.Opacity = 1;
            args.Handled = true;
        };
        handle.PointerMoved += (_, args) =>
        {
            if (!isDragging)
            {
                return;
            }

            double currentX = args.GetCurrentPoint(root).Position.X;
            double width = Math.Clamp(startWidth + currentX - startX, MinimumPreviewPaneWidth, MaximumPreviewPaneWidth);
            previewColumn.Width = new GridLength(width);
            args.Handled = true;
        };
        handle.PointerReleased += (_, args) =>
        {
            isDragging = false;
            handle.ReleasePointerCapture(args.Pointer);
            line.Opacity = 0.65;
            args.Handled = true;
        };
        handle.PointerCanceled += (_, args) =>
        {
            isDragging = false;
            handle.ReleasePointerCapture(args.Pointer);
            line.Opacity = 0.65;
        };
        handle.PointerCaptureLost += (_, _) =>
        {
            isDragging = false;
            line.Opacity = 0.65;
        };

        return handle;
    }

    private UIElement BuildMonitorSettings(MonitorProfile profile)
    {
        var root = new Grid
        {
            RowSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch,
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

}
