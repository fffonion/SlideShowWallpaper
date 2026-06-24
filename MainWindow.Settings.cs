using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using SlideShowWallpaper.Windows;

namespace SlideShowWallpaper;

public sealed partial class MainWindow
{
    private readonly record struct HardwareEditorSnapResult(double Left, double Top, double? VerticalGuide, double? HorizontalGuide);

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
            new SettingsRow(LocalizedStrings.Get("AppSettingAutostart"), CreateAutostartControls()),
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
        Grid.SetRow(form, 0);
        root.Children.Add(form);
        StartThumbnailCacheSizeLoad();
        return new ScrollViewer
        {
            Content = root,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private FrameworkElement CreateAutostartControls()
    {
        var panel = new StackPanel
        {
            Spacing = 6,
        };
        var autostartCheckBox = new CheckBox
        {
            Content = LocalizedStrings.Get("AppSettingAutostart"),
            IsChecked = _viewModel.StartWithWindows,
        };
        var adminCheckBox = new CheckBox
        {
            Content = LocalizedStrings.Get("AppSettingAutostartAsAdministrator"),
            IsChecked = _viewModel.StartWithWindowsAsAdministrator,
            IsEnabled = _viewModel.StartWithWindows,
        };
        AutomationProperties.SetName(autostartCheckBox, LocalizedStrings.Get("AppSettingAutostart"));
        AutomationProperties.SetName(adminCheckBox, LocalizedStrings.Get("AppSettingAutostartAsAdministrator"));

        void ApplyAutostart()
        {
            bool startWithWindows = autostartCheckBox.IsChecked == true;
            bool runAsAdministrator = adminCheckBox.IsChecked == true;
            try
            {
                _viewModel.StartWithWindows = startWithWindows;
                _viewModel.StartWithWindowsAsAdministrator = runAsAdministrator;
                _autostartService.SetEnabled(startWithWindows, runAsAdministrator);
                adminCheckBox.IsEnabled = startWithWindows;
                ApplySettings();
            }
            catch (Exception exception)
            {
                AppLog.Write(exception);
                _viewModel.StartWithWindows = _autostartService.IsEnabled();
                _viewModel.StartWithWindowsAsAdministrator = _autostartService.IsRunAsAdministratorEnabled();
                RenderTabs(_selectedMonitorId);
            }
        }

        autostartCheckBox.Checked += (_, _) => ApplyAutostart();
        autostartCheckBox.Unchecked += (_, _) => ApplyAutostart();
        adminCheckBox.Checked += (_, _) => ApplyAutostart();
        adminCheckBox.Unchecked += (_, _) => ApplyAutostart();
        panel.Children.Add(autostartCheckBox);
        panel.Children.Add(adminCheckBox);
        return panel;
    }

    private UIElement BuildHardwareMonitorSettingsPage()
    {
        HardwareMonitorConfig config = _viewModel.HardwareMonitor;
        RefreshHardwareSnapshot();
        EnsureDefaultHardwareSensors(config);

        var form = new StackPanel
        {
            Spacing = 14,
        };
        form.Children.Add(CreateHardwareMonitorSettingsSection());

        return new ScrollViewer
        {
            Content = form,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    private UIElement BuildHardwareEditorPage()
    {
        HardwareMonitorConfig config = _viewModel.HardwareMonitor;
        RefreshHardwareSnapshot();
        EnsureDefaultHardwareSensors(config);

        var root = new Grid
        {
            ColumnSpacing = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 640 });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(430), MinWidth = 390 });

        FrameworkElement previewSection = CreateHardwareEditorPreviewSection(config);
        Grid.SetColumn(previewSection, 0);
        root.Children.Add(previewSection);

        var formatStack = new StackPanel
        {
            Spacing = 14,
        };
        formatStack.Children.Add(CreateHardwareOverlayFormatSection(config));
        formatStack.Children.Add(CreateHardwareElementSettingsSection(config));

        var formatScrollViewer = new ScrollViewer
        {
            Content = formatStack,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetColumn(formatScrollViewer, 1);
        root.Children.Add(formatScrollViewer);
        return root;
    }

    private Border CreateHardwareMonitorSettingsSection()
    {
        HardwareMonitorConfig config = _viewModel.HardwareMonitor;

        FrameworkElement sensorList = CreateHardwareSensorSelectionList(config);
        return CreateSettingsSection(
            LocalizedStrings.Get("HardwareMonitorSettingsGroup"),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorEnabled"), CreateCheckBox(config.IsEnabled, value => config.IsEnabled = value, LocalizedStrings.Get("HardwareMonitorEnabled"))),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorRefreshInterval"), CreateNumberBox(config.RefreshIntervalSeconds, value => config.RefreshIntervalSeconds = Math.Max(1, (int)Math.Round(value)), LocalizedStrings.Get("HardwareMonitorRefreshInterval"))),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorTargetDisplay"), CreateChoiceCombo(CreateHardwareMonitorTargetChoices(), config.TargetMonitorId, value => config.TargetMonitorId = value, LocalizedStrings.Get("HardwareMonitorTargetDisplay"))),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorSensors"), sensorList, IsFullWidth: true));
    }

    private Border CreateHardwareOverlayFormatSection(HardwareMonitorConfig config)
    {
        TextBox templateBox = CreateHardwareTemplateTextBox(config);
        FrameworkElement buttonRow = CreateHardwareTemplateButtons(config);
        return CreateSettingsSection(
            LocalizedStrings.Get("HardwareMonitorStyle"),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorPosition"), CreateHardwarePositionControls(config)),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorFontFamily"), CreateHardwareFontCombo(config.FontFamily, value => config.FontFamily = string.IsNullOrWhiteSpace(value) ? "Segoe UI" : value, LocalizedStrings.Get("HardwareMonitorFontFamily"))),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorFontSize"), CreateNumberBox(config.FontSize, value => config.FontSize = Math.Max(10, value), LocalizedStrings.Get("HardwareMonitorFontSize"))),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorOpacityShort"), CreateNumberBox(config.Opacity, value => config.Opacity = Math.Clamp(value, 0.1, 1), LocalizedStrings.Get("HardwareMonitorOpacityShort"))),
            new SettingsRow(LocalizedStrings.Get("HardwareMonitorTemplate"), templateBox),
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

    private TextBox CreateHardwareTemplateTextBox(HardwareMonitorConfig config)
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
            ScheduleApplySettings();
        };
        return textBox;
    }

    private FrameworkElement CreateHardwareSensorSelectionList(HardwareMonitorConfig config)
    {
        var root = new StackPanel
        {
            Spacing = 8,
        };
        HardwareMonitorSnapshot? snapshot = _hardwareMonitorSnapshot;
        IReadOnlyList<HardwareSensorReading> sensors = (snapshot?.Sensors ?? [])
            .OrderBy(sensor => sensor.Group)
            .ThenBy(sensor => sensor.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        if (snapshot is { IsElevated: false })
        {
            root.Children.Add(CreateHardwareSensorNotice());
        }

        if (sensors.Count == 0)
        {
            root.Children.Add(new TextBlock
            {
                Text = LocalizedStrings.Get("HardwareMonitorNoSensors"),
                TextWrapping = TextWrapping.Wrap,
            });
            return root;
        }

        var searchBox = new TextBox
        {
            PlaceholderText = LocalizedStrings.Get("HardwareMonitorSearchSensors"),
        };
        AutomationProperties.SetName(searchBox, LocalizedStrings.Get("HardwareMonitorSearchSensors"));
        root.Children.Add(searchBox);

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        var selectAllButton = new Button
        {
            Content = LocalizedStrings.Get("HardwareMonitorSelectAll"),
        };
        var invertButton = new Button
        {
            Content = LocalizedStrings.Get("HardwareMonitorInvertSelection"),
        };
        var refreshButton = new Button
        {
            Content = new SymbolIcon(Symbol.Refresh),
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
        };
        AutomationProperties.SetName(selectAllButton, LocalizedStrings.Get("HardwareMonitorSelectAll"));
        AutomationProperties.SetName(invertButton, LocalizedStrings.Get("HardwareMonitorInvertSelection"));
        AutomationProperties.SetName(refreshButton, LocalizedStrings.Get("RefreshHardwareSensors"));
        ToolTipService.SetToolTip(refreshButton, LocalizedStrings.Get("RefreshHardwareSensors"));
        buttonRow.Children.Add(refreshButton);
        buttonRow.Children.Add(selectAllButton);
        buttonRow.Children.Add(invertButton);
        root.Children.Add(buttonRow);

        var stack = new StackPanel
        {
            Spacing = 4,
        };
        root.Children.Add(new ScrollViewer
        {
            Content = stack,
            MaxHeight = 260,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        });

        IReadOnlyList<HardwareSensorReading> GetFilteredSensors()
        {
            string filter = searchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(filter))
            {
                return sensors;
            }

            return sensors
                .Where(sensor => sensor.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                    || GetHardwareMetricGroupLabel(sensor.Group).Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                .ToArray();
        }

        void RenderSensorList()
        {
            stack.Children.Clear();
            foreach (HardwareSensorReading sensor in GetFilteredSensors())
            {
                var checkBox = new CheckBox
                {
                    Content = $"{GetHardwareMetricGroupLabel(sensor.Group)} · {sensor.DisplayName}",
                    IsChecked = config.SelectedSensorIds.Contains(sensor.Id, StringComparer.OrdinalIgnoreCase),
                };
                checkBox.Content = CreateHardwareSensorSelectionContent(sensor);
                AutomationProperties.SetName(checkBox, sensor.DisplayName);
                checkBox.Checked += (_, _) =>
                {
                    if (!config.SelectedSensorIds.Contains(sensor.Id, StringComparer.OrdinalIgnoreCase))
                    {
                        config.SelectedSensorIds.Add(sensor.Id);
                    }

                    ScheduleApplySettings();
                };
                checkBox.Unchecked += (_, _) =>
                {
                    config.SelectedSensorIds.RemoveAll(id => string.Equals(id, sensor.Id, StringComparison.OrdinalIgnoreCase));
                    ScheduleApplySettings();
                };
                stack.Children.Add(checkBox);
            }
        }

        searchBox.TextChanged += (_, _) => RenderSensorList();
        refreshButton.Click += (_, _) =>
        {
            RefreshHardwareSnapshot();
            RenderTabs(_selectedMonitorId);
        };
        selectAllButton.Click += (_, _) =>
        {
            foreach (HardwareSensorReading sensor in GetFilteredSensors())
            {
                if (!config.SelectedSensorIds.Contains(sensor.Id, StringComparer.OrdinalIgnoreCase))
                {
                    config.SelectedSensorIds.Add(sensor.Id);
                }
            }

            ScheduleApplySettings();
            RenderSensorList();
        };
        invertButton.Click += (_, _) =>
        {
            foreach (HardwareSensorReading sensor in GetFilteredSensors())
            {
                if (config.SelectedSensorIds.Contains(sensor.Id, StringComparer.OrdinalIgnoreCase))
                {
                    config.SelectedSensorIds.RemoveAll(id => string.Equals(id, sensor.Id, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    config.SelectedSensorIds.Add(sensor.Id);
                }
            }

            ScheduleApplySettings();
            RenderSensorList();
        };
        RenderSensorList();
        return root;
    }

    private FrameworkElement CreateHardwareSensorNotice()
    {
        var grid = new Grid
        {
            ColumnSpacing = 12,
            Margin = new Thickness(0, 0, 0, 6),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new TextBlock
        {
            Text = LocalizedStrings.Get("HardwareMonitorLimitedAccess"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetThemeBrush("TextFillColorSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var restartButton = new Button
        {
            Content = LocalizedStrings.Get("HardwareMonitorRestartAsAdministrator"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(restartButton, LocalizedStrings.Get("HardwareMonitorRestartAsAdministrator"));
        restartButton.Click += (_, _) => RestartAsAdministrator();
        Grid.SetColumn(restartButton, 1);
        grid.Children.Add(restartButton);
        return grid;
    }

    private void RestartAsAdministrator()
    {
        if (!_administratorRestartService.TryRestart())
        {
            return;
        }

        ExitApplication();
    }

    private static FrameworkElement CreateHardwareSensorSelectionContent(HardwareSensorReading sensor)
    {
        var row = new Grid
        {
            ColumnSpacing = 0,
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Canvas icon = HardwareOverlayIconFactory.CreateIcon(
            GetHardwareMetricKindIcon(sensor.Kind),
            18,
            GetThemeBrush("TextFillColorPrimaryBrush"));
        icon.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(icon, 0);
        row.Children.Add(icon);

        var sensorText = new TextBlock
        {
            Text = $" {GetHardwareMetricGroupLabel(sensor.Group)} - {sensor.DisplayName}",
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(sensorText, 1);
        row.Children.Add(sensorText);
        return row;
    }

    private static HardwareOverlayIconKind GetHardwareMetricKindIcon(HardwareMetricKind kind)
    {
        return kind switch
        {
            HardwareMetricKind.Temperature => HardwareOverlayIconKind.Temperature,
            HardwareMetricKind.FanRpm => HardwareOverlayIconKind.Fan,
            HardwareMetricKind.MemoryAvailable => HardwareOverlayIconKind.Memory,
            HardwareMetricKind.VramAvailable => HardwareOverlayIconKind.Vram,
            HardwareMetricKind.Power => HardwareOverlayIconKind.Power,
            _ => HardwareOverlayIconKind.Generic,
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

    private FrameworkElement CreateHardwareEditorPreviewSection(HardwareMonitorConfig config)
    {
        var stack = new StackPanel
        {
            Spacing = 12,
            Padding = new Thickness(16),
        };

        var title = new TextBlock
        {
            Text = LocalizedStrings.Get("HardwareMonitorEditor"),
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        AutomationProperties.SetName(title, title.Text);
        stack.Children.Add(title);
        stack.Children.Add(CreateHardwareEditorActions(config));
        stack.Children.Add(CreateHardwareEditorPreviewSurface(config));

        return new Border
        {
            Background = GetThemeBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = stack,
        };
    }

    private FrameworkElement CreateHardwareEditorActions(HardwareMonitorConfig config)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        var addSensorsButton = new Button
        {
            Content = LocalizedStrings.Get("HardwareMonitorAddSensorElements"),
        };
        addSensorsButton.Click += (_, _) =>
        {
            AddSelectedHardwareSensorsToEditor(config);
            ScheduleApplySettings();
            RenderTabs(_selectedMonitorId);
        };

        var addTextButton = new Button
        {
            Content = LocalizedStrings.Get("HardwareMonitorAddTextElement"),
        };
        addTextButton.Click += (_, _) =>
        {
            HardwareOverlayElement element = CreateDefaultHardwareElement(HardwareOverlayElementKind.Text, config.Elements.Count);
            element.Text = LocalizedStrings.Get("HardwareMonitorTextElementDefault");
            config.Elements.Add(element);
            config.SelectedElementId = element.Id;
            ScheduleApplySettings();
            RenderTabs(_selectedMonitorId);
        };

        var imageButton = new Button
        {
            Content = new SymbolIcon(Symbol.OpenFile),
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
        };
        AutomationProperties.SetName(imageButton, LocalizedStrings.Get("HardwareMonitorImportIconImage"));
        ToolTipService.SetToolTip(imageButton, LocalizedStrings.Get("HardwareMonitorImportIconImage"));
        imageButton.Click += async (_, _) => await ImportHardwareElementImageAsync(config);

        var backgroundButton = new Button
        {
            Content = new SymbolIcon(Symbol.Pictures),
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
        };
        AutomationProperties.SetName(backgroundButton, LocalizedStrings.Get("HardwareMonitorImportBackground"));
        ToolTipService.SetToolTip(backgroundButton, LocalizedStrings.Get("HardwareMonitorImportBackground"));
        backgroundButton.Click += async (_, _) => await ImportHardwareBackgroundAsync(config);

        var clearBackgroundButton = new Button
        {
            Content = new SymbolIcon(Symbol.Clear),
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
        };
        AutomationProperties.SetName(clearBackgroundButton, LocalizedStrings.Get("HardwareMonitorClearBackground"));
        ToolTipService.SetToolTip(clearBackgroundButton, LocalizedStrings.Get("HardwareMonitorClearBackground"));
        clearBackgroundButton.Click += (_, _) =>
        {
            config.BackgroundImagePath = string.Empty;
            ScheduleApplySettings();
            RenderTabs(_selectedMonitorId);
        };

        panel.Children.Add(addSensorsButton);
        panel.Children.Add(addTextButton);
        panel.Children.Add(imageButton);
        panel.Children.Add(backgroundButton);
        panel.Children.Add(clearBackgroundButton);
        return panel;
    }

    private FrameworkElement CreateHardwareEditorPreviewSurface(HardwareMonitorConfig config)
    {
        HardwareMonitorSnapshot snapshot = _hardwareMonitorSnapshot ?? new HardwareMonitorSnapshot([], DateTimeOffset.Now);
        IReadOnlyList<HardwareOverlayElementState> elements = HardwareOverlayTextRenderer.CreateElementStates(config, snapshot);
        bool hasBackgroundSize = ImageDimensionReader.TryRead(config.BackgroundImagePath, out int backgroundWidth, out int backgroundHeight);
        HardwareOverlayLayout layout = hasBackgroundSize || elements.Count > 0
            ? HardwareOverlayLayoutCalculator.Calculate(elements, backgroundWidth, backgroundHeight)
            : new HardwareOverlayLayout(720, 420);
        var canvas = new Canvas
        {
            Width = layout.Width,
            Height = layout.Height,
            Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 18, 22, 26)),
        };
        AutomationProperties.SetName(canvas, LocalizedStrings.Get("HardwareMonitorPreview"));
        if (TryCreateSettingsBitmapImage(config.BackgroundImagePath, out BitmapImage? background))
        {
            canvas.Children.Add(new Microsoft.UI.Xaml.Controls.Image
            {
                Source = background,
                Width = canvas.Width,
                Height = canvas.Height,
                Stretch = Stretch.UniformToFill,
            });
        }

        Line verticalGuide = CreateHardwareEditorGuideLine(isVertical: true, canvas.Width, canvas.Height);
        Line horizontalGuide = CreateHardwareEditorGuideLine(isVertical: false, canvas.Width, canvas.Height);
        foreach (HardwareOverlayElement element in config.Elements)
        {
            HardwareOverlayElementState state = elements.FirstOrDefault(item => string.Equals(item.Id, element.Id, StringComparison.OrdinalIgnoreCase))
                ?? new HardwareOverlayElementState(
                    element.Id,
                    element.Kind,
                    element.Text,
                    element.ImagePath,
                    HardwareOverlayIconKind.Generic,
                    element.X,
                    element.Y,
                    element.Width,
                    element.Height,
                    string.IsNullOrWhiteSpace(element.FontFamily) ? config.FontFamily : element.FontFamily,
                    element.FontSize > 0 ? element.FontSize : config.FontSize,
                    element.Foreground,
                    element.Opacity);
            FrameworkElement visual = CreateHardwareEditorElementVisual(state, string.Equals(config.SelectedElementId, element.Id, StringComparison.OrdinalIgnoreCase));
            AttachHardwareEditorDrag(canvas, visual, element, config, verticalGuide, horizontalGuide);
            (double visualWidth, double visualHeight) = GetHardwareEditorVisualSize(visual, canvas.Width, canvas.Height);
            Canvas.SetLeft(visual, Math.Clamp(element.X, 0, Math.Max(0, canvas.Width - visualWidth)));
            Canvas.SetTop(visual, Math.Clamp(element.Y, 0, Math.Max(0, canvas.Height - visualHeight)));
            canvas.Children.Add(visual);
        }

        canvas.Children.Add(verticalGuide);
        canvas.Children.Add(horizontalGuide);

        return new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8),
            Background = GetThemeBrush("LayerFillColorDefaultBrush"),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = canvas,
        };
    }

    private Border CreateHardwareElementSettingsSection(HardwareMonitorConfig config)
    {
        HardwareOverlayElement? element = config.Elements.FirstOrDefault(item => string.Equals(item.Id, config.SelectedElementId, StringComparison.OrdinalIgnoreCase));
        if (element is null)
        {
            return CreateSettingsContentSection(
                LocalizedStrings.Get("HardwareMonitorElementSettings"),
                new TextBlock
                {
                    Text = LocalizedStrings.Get("HardwareMonitorNoElementSelected"),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = GetThemeBrush("TextFillColorSecondaryBrush"),
                });
        }

        return CreateSettingsContentSection(
            LocalizedStrings.Get("HardwareMonitorElementSettings"),
            CreateHardwareElementSettings(config, element));
    }

    private FrameworkElement CreateHardwareElementSettings(HardwareMonitorConfig config, HardwareOverlayElement element)
    {
        var stack = new StackPanel
        {
            Spacing = 8,
        };

        if (element.Kind == HardwareOverlayElementKind.Sensor)
        {
            stack.Children.Add(CreateCompactEditorRow(
                LocalizedStrings.Get("HardwareMonitorSensors"),
                new TextBlock
                {
                    Text = GetHardwareElementSensorDisplayName(config, element),
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                }));
        }

        if (element.Kind == HardwareOverlayElementKind.Text)
        {
            var textBox = new TextBox
            {
                Text = element.Text,
                PlaceholderText = LocalizedStrings.Get("HardwareMonitorTextElementDefault"),
            };
            AutomationProperties.SetName(textBox, LocalizedStrings.Get("HardwareMonitorElementText"));
            textBox.TextChanged += (_, _) =>
            {
                element.Text = textBox.Text;
                ScheduleApplySettings();
            };
            stack.Children.Add(CreateCompactEditorRow(LocalizedStrings.Get("HardwareMonitorElementText"), textBox));
        }

        if (element.Kind != HardwareOverlayElementKind.Image)
        {
            stack.Children.Add(CreateCompactEditorRow(LocalizedStrings.Get("HardwareMonitorElementFontFamily"), CreateHardwareFontCombo(string.IsNullOrWhiteSpace(element.FontFamily) ? config.FontFamily : element.FontFamily, value => element.FontFamily = value, LocalizedStrings.Get("HardwareMonitorElementFontFamily"))));
            stack.Children.Add(CreateCompactEditorRow(LocalizedStrings.Get("HardwareMonitorElementFontSize"), CreateNumberBox(element.FontSize, value => element.FontSize = Math.Max(8, value), LocalizedStrings.Get("HardwareMonitorElementFontSize"))));
            stack.Children.Add(CreateCompactEditorRow(LocalizedStrings.Get("HardwareMonitorElementForeground"), CreateHardwareColorPicker(element.Foreground, value => element.Foreground = value, LocalizedStrings.Get("HardwareMonitorElementForeground"))));
        }

        stack.Children.Add(CreateCompactEditorRow(LocalizedStrings.Get("HardwareMonitorPosition"), CreateHardwareElementPositionControls(element)));
        stack.Children.Add(CreateCompactEditorRow(LocalizedStrings.Get("HardwareMonitorElementSize"), CreateHardwareElementSizeControls(element)));
        stack.Children.Add(CreateCompactEditorRow(LocalizedStrings.Get("HardwareMonitorElementOpacity"), CreateNumberBox(element.Opacity, value => element.Opacity = Math.Clamp(value, 0.05, 1), LocalizedStrings.Get("HardwareMonitorElementOpacity"))));

        var deleteButton = new Button
        {
            Content = LocalizedStrings.Get("HardwareMonitorDeleteElement"),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        deleteButton.Click += (_, _) =>
        {
            config.Elements.RemoveAll(item => string.Equals(item.Id, element.Id, StringComparison.OrdinalIgnoreCase));
            config.SelectedElementId = config.Elements.FirstOrDefault()?.Id ?? string.Empty;
            ScheduleApplySettings();
            RenderTabs(_selectedMonitorId);
        };
        stack.Children.Add(deleteButton);
        return stack;
    }

    private string GetHardwareElementSensorDisplayName(HardwareMonitorConfig config, HardwareOverlayElement element)
    {
        HardwareSensorReading? sensor = _hardwareMonitorSnapshot?.Sensors.FirstOrDefault(item => string.Equals(item.Id, element.SensorId, StringComparison.OrdinalIgnoreCase));
        if (sensor is not null)
        {
            return sensor.DisplayName;
        }

        return string.IsNullOrWhiteSpace(element.Text)
            ? element.SensorId
            : element.Text;
    }

    private static Border CreateCompactEditorRow(string label, FrameworkElement control)
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxLines = 2,
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return new Border
        {
            Child = grid,
        };
    }

    private TextBox CreateHardwareTextBox(string value, Action<string> changed, string automationName)
    {
        var textBox = new TextBox
        {
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(textBox, automationName);
        textBox.TextChanged += (_, _) =>
        {
            changed(textBox.Text);
            ScheduleApplySettings();
        };
        return textBox;
    }

    private static ComboBoxItem CreateHardwareFontComboItem(string font)
    {
        return new ComboBoxItem
        {
            Tag = font,
            Content = new TextBlock
            {
                Text = font,
                FontFamily = new FontFamily(font),
                FontSize = 16,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };
    }

    private static IEnumerable<string> MergeSelectedFont(string selectedFont, IReadOnlyList<string> fonts)
    {
        if (!fonts.Contains(selectedFont, StringComparer.OrdinalIgnoreCase))
        {
            yield return selectedFont;
        }

        foreach (string font in fonts)
        {
            yield return font;
        }
    }

    private FrameworkElement CreateHardwareFontCombo(string value, Action<string> changed, string automationName)
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        string selectedFont = string.IsNullOrWhiteSpace(value) ? "Segoe UI" : value;
        var combo = new ComboBox
        {
            MinWidth = 160,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        combo.Items.Add(CreateHardwareFontComboItem(selectedFont));
        combo.SelectedIndex = 0;
        AutomationProperties.SetName(combo, automationName);
        Grid.SetColumn(combo, 0);
        grid.Children.Add(combo);

        var progress = new ProgressRing
        {
            Width = 18,
            Height = 18,
            IsActive = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(progress, automationName);
        Grid.SetColumn(progress, 1);
        grid.Children.Add(progress);

        bool loaded = false;
        bool loading = false;
        combo.DropDownOpened += async (_, _) =>
        {
            if (loaded || loading)
            {
                return;
            }

            loading = true;
            progress.IsActive = true;
            progress.Visibility = Visibility.Visible;
            try
            {
                IReadOnlyList<string> fonts = await Task.Run(FontCatalogService.GetInstalledFontFamilies);
                combo.Items.Clear();
                foreach (string font in MergeSelectedFont(selectedFont, fonts))
                {
                    combo.Items.Add(CreateHardwareFontComboItem(font));
                }

                combo.SelectedItem = combo.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(item => string.Equals(item.Tag as string, selectedFont, StringComparison.OrdinalIgnoreCase));
                loaded = true;
            }
            catch (Exception exception)
            {
                AppLog.Write(exception);
            }
            finally
            {
                loading = false;
                progress.IsActive = false;
                progress.Visibility = Visibility.Collapsed;
            }
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem { Tag: string font }
                && !string.Equals(font, selectedFont, StringComparison.Ordinal))
            {
                selectedFont = font;
                changed(font);
                ScheduleApplySettings();
            }
        };
        return grid;
    }

    private FrameworkElement CreateHardwareColorPicker(string value, Action<string> changed, string automationName)
    {
        global::Windows.UI.Color color = ParseSettingsColor(value);
        var swatch = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush"),
            Background = new SolidColorBrush(color),
        };
        var colorText = new TextBlock
        {
            Text = FormatSettingsColor(color),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var buttonContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
        };
        buttonContent.Children.Add(swatch);
        buttonContent.Children.Add(colorText);

        var picker = new ColorPicker
        {
            Color = color,
            IsAlphaEnabled = true,
            IsAlphaSliderVisible = true,
            IsColorChannelTextInputVisible = true,
            IsHexInputVisible = true,
            IsMoreButtonVisible = false,
            MinWidth = 320,
        };
        AutomationProperties.SetName(picker, automationName);
        picker.ColorChanged += (_, args) =>
        {
            string hex = FormatSettingsColor(args.NewColor);
            swatch.Background = new SolidColorBrush(args.NewColor);
            colorText.Text = hex;
            changed(hex);
            ScheduleApplySettings();
        };

        var button = new Button
        {
            Content = buttonContent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Flyout = new Flyout
            {
                Content = picker,
                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            },
        };
        AutomationProperties.SetName(button, automationName);
        return button;
    }

    private FrameworkElement CreateHardwareElementPositionControls(HardwareOverlayElement element)
    {
        var panel = new Grid
        {
            ColumnSpacing = 8,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        FrameworkElement xControl = CreateLabeledNumberBox("X", element.X, value => element.X = Math.Max(0, value));
        FrameworkElement yControl = CreateLabeledNumberBox("Y", element.Y, value => element.Y = Math.Max(0, value));
        Grid.SetColumn(xControl, 0);
        Grid.SetColumn(yControl, 1);
        panel.Children.Add(xControl);
        panel.Children.Add(yControl);
        return panel;
    }

    private FrameworkElement CreateHardwareElementSizeControls(HardwareOverlayElement element)
    {
        var panel = new Grid
        {
            ColumnSpacing = 8,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        FrameworkElement widthControl = CreateLabeledNumberBox("W", element.Width, value => element.Width = Math.Max(20, value));
        FrameworkElement heightControl = CreateLabeledNumberBox("H", element.Height, value => element.Height = Math.Max(20, value));
        Grid.SetColumn(widthControl, 0);
        Grid.SetColumn(heightControl, 1);
        panel.Children.Add(widthControl);
        panel.Children.Add(heightControl);
        return panel;
    }

    private async Task ImportHardwareElementImageAsync(HardwareMonitorConfig config)
    {
        string? path = await _folderPickerService.PickOpenFileAsync(_hwnd, [".png", ".jpg", ".jpeg", ".bmp", ".webp"]);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        HardwareOverlayElement element = CreateDefaultHardwareElement(HardwareOverlayElementKind.Image, config.Elements.Count);
        element.ImagePath = path;
        element.Width = 48;
        element.Height = 48;
        config.Elements.Add(element);
        config.SelectedElementId = element.Id;
        ScheduleApplySettings();
        RenderTabs(_selectedMonitorId);
    }

    private async Task ImportHardwareBackgroundAsync(HardwareMonitorConfig config)
    {
        string? path = await _folderPickerService.PickOpenFileAsync(_hwnd, [".png", ".jpg", ".jpeg", ".bmp", ".webp"]);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        config.BackgroundImagePath = path;
        ScheduleApplySettings();
        RenderTabs(_selectedMonitorId);
    }

    private void AddSelectedHardwareSensorsToEditor(HardwareMonitorConfig config)
    {
        HardwareMonitorSnapshot snapshot = _hardwareMonitorSnapshot ?? new HardwareMonitorSnapshot([], DateTimeOffset.Now);
        IReadOnlyList<HardwareSensorReading> selectedSensors = HardwareOverlayTextRenderer.SelectSensors(config, snapshot);
        foreach (HardwareSensorReading sensor in selectedSensors)
        {
            if (config.Elements.Any(element => string.Equals(element.SensorId, sensor.Id, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            HardwareOverlayElement element = CreateDefaultHardwareElement(HardwareOverlayElementKind.Sensor, config.Elements.Count);
            element.SensorId = sensor.Id;
            element.Text = sensor.DisplayName;
            config.Elements.Add(element);
            config.SelectedElementId = element.Id;
        }
    }

    private static HardwareOverlayElement CreateDefaultHardwareElement(HardwareOverlayElementKind kind, int index)
    {
        (double x, double y) = GetDefaultHardwareElementPosition(kind, index);
        return new HardwareOverlayElement
        {
            Kind = kind,
            X = x,
            Y = y,
            Width = kind == HardwareOverlayElementKind.Image ? 48 : 176,
            Height = kind == HardwareOverlayElementKind.Image ? 48 : 28,
            Foreground = "#FFFFFFFF",
            Opacity = 1,
        };
    }

    private static (double X, double Y) GetDefaultHardwareElementPosition(HardwareOverlayElementKind kind, int index)
    {
        if (kind != HardwareOverlayElementKind.Sensor)
        {
            return (16, 16 + (index * 40));
        }

        const int rowsPerColumn = 6;
        const double left = 16;
        const double top = 18;
        const double columnWidth = 188;
        const double rowHeight = 31;
        return (left + ((index / rowsPerColumn) * columnWidth), top + ((index % rowsPerColumn) * rowHeight));
    }

    private FrameworkElement CreateHardwareEditorElementVisual(HardwareOverlayElementState element, bool isSelected)
    {
        FrameworkElement child;
        if (element.Kind == HardwareOverlayElementKind.Image && TryCreateSettingsBitmapImage(element.ImagePath, out BitmapImage? bitmap))
        {
            child = new Microsoft.UI.Xaml.Controls.Image
            {
                Source = bitmap,
                Stretch = Stretch.UniformToFill,
            };
        }
        else if (element.Kind == HardwareOverlayElementKind.Sensor)
        {
            child = CreateHardwareEditorSensorVisual(element);
        }
        else
        {
            child = new TextBlock
            {
                Text = element.Text,
                FontFamily = new FontFamily(element.FontFamily),
                FontSize = Math.Max(8, element.FontSize),
                Foreground = CreateSettingsColorBrush(element.Foreground),
                TextWrapping = TextWrapping.WrapWholeWords,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        var host = new Border
        {
            Opacity = element.Opacity,
            Background = isSelected
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(36, 255, 255, 255))
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderBrush = isSelected
                ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 96, 205, 255))
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(isSelected ? 2 : 0),
            Padding = element.Kind == HardwareOverlayElementKind.Image ? new Thickness(0) : new Thickness(4, 1, 4, 1),
            Child = child,
        };
        if (element.Kind == HardwareOverlayElementKind.Image)
        {
            host.Width = element.Width;
            host.Height = element.Height;
        }

        AutomationProperties.SetName(host, element.Text);
        return host;
    }

    private static (double Width, double Height) GetHardwareEditorVisualSize(FrameworkElement visual, double availableWidth, double availableHeight)
    {
        if (visual.ActualWidth > 0 && visual.ActualHeight > 0)
        {
            return (visual.ActualWidth, visual.ActualHeight);
        }

        visual.Measure(new global::Windows.Foundation.Size(Math.Max(1, availableWidth), Math.Max(1, availableHeight)));
        return (Math.Max(1, visual.DesiredSize.Width), Math.Max(1, visual.DesiredSize.Height));
    }

    private static FrameworkElement CreateHardwareEditorSensorVisual(HardwareOverlayElementState element)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(HardwareOverlayIconFactory.CreateIcon(element.IconKind, Math.Max(18, element.FontSize + 2), CreateSettingsColorBrush(element.Foreground)));
        row.Children.Add(new TextBlock
        {
            Text = element.Text,
            FontFamily = new FontFamily(element.FontFamily),
            FontSize = Math.Max(8, element.FontSize),
            Foreground = CreateSettingsColorBrush(element.Foreground),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    private static Line CreateHardwareEditorGuideLine(bool isVertical, double canvasWidth, double canvasHeight)
    {
        var line = new Line
        {
            Stroke = GetThemeBrush("AccentFillColorDefaultBrush"),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        if (isVertical)
        {
            line.Y1 = 0;
            line.Y2 = canvasHeight;
        }
        else
        {
            line.X1 = 0;
            line.X2 = canvasWidth;
        }

        return line;
    }

    private void AttachHardwareEditorDrag(
        Canvas canvas,
        FrameworkElement visual,
        HardwareOverlayElement element,
        HardwareMonitorConfig config,
        Line verticalGuide,
        Line horizontalGuide)
    {
        bool isDragging = false;
        double dragStartX = 0;
        double dragStartY = 0;
        double startLeft = 0;
        double startTop = 0;

        visual.Tapped += (_, _) =>
        {
            config.SelectedElementId = element.Id;
            RenderTabs(_selectedMonitorId);
        };
        visual.PointerPressed += (_, args) =>
        {
            isDragging = true;
            config.SelectedElementId = element.Id;
            dragStartX = args.GetCurrentPoint(canvas).Position.X;
            dragStartY = args.GetCurrentPoint(canvas).Position.Y;
            startLeft = Canvas.GetLeft(visual);
            startTop = Canvas.GetTop(visual);
            visual.CapturePointer(args.Pointer);
            HideHardwareEditorGuides(verticalGuide, horizontalGuide);
            args.Handled = true;
        };
        visual.PointerMoved += (_, args) =>
        {
            if (!isDragging)
            {
                return;
            }

            global::Windows.Foundation.Point point = args.GetCurrentPoint(canvas).Position;
            (double visualWidth, double visualHeight) = GetHardwareEditorVisualSize(visual, canvas.Width, canvas.Height);
            double left = Math.Clamp(startLeft + point.X - dragStartX, 0, Math.Max(0, canvas.Width - visualWidth));
            double top = Math.Clamp(startTop + point.Y - dragStartY, 0, Math.Max(0, canvas.Height - visualHeight));
            HardwareEditorSnapResult snap = ApplyHardwareEditorSnap(config.Elements, element, left, top, visualWidth, visualHeight, canvas.Width, canvas.Height);
            left = snap.Left;
            top = snap.Top;
            Canvas.SetLeft(visual, left);
            Canvas.SetTop(visual, top);
            element.X = left;
            element.Y = top;
            UpdateHardwareEditorGuides(verticalGuide, horizontalGuide, snap.VerticalGuide, snap.HorizontalGuide, canvas.Width, canvas.Height);
            ScheduleApplySettings();
            args.Handled = true;
        };
        visual.PointerReleased += (_, args) =>
        {
            isDragging = false;
            visual.ReleasePointerCapture(args.Pointer);
            HideHardwareEditorGuides(verticalGuide, horizontalGuide);
            args.Handled = true;
        };
        visual.PointerCanceled += (_, args) =>
        {
            isDragging = false;
            visual.ReleasePointerCapture(args.Pointer);
            HideHardwareEditorGuides(verticalGuide, horizontalGuide);
        };
        visual.PointerCaptureLost += (_, _) =>
        {
            isDragging = false;
            HideHardwareEditorGuides(verticalGuide, horizontalGuide);
        };
    }

    private static HardwareEditorSnapResult ApplyHardwareEditorSnap(
        IReadOnlyList<HardwareOverlayElement> elements,
        HardwareOverlayElement activeElement,
        double left,
        double top,
        double width,
        double height,
        double canvasWidth,
        double canvasHeight)
    {
        double maxLeft = Math.Max(0, canvasWidth - width);
        double maxTop = Math.Max(0, canvasHeight - height);
        double snappedLeft = SnapHardwareEditorAxis(
            left,
            width,
            maxLeft,
            elements.Where(element => !ReferenceEquals(element, activeElement)).Select(element => (element.X, element.Width)),
            out double? verticalGuide);
        double snappedTop = SnapHardwareEditorAxis(
            top,
            height,
            maxTop,
            elements.Where(element => !ReferenceEquals(element, activeElement)).Select(element => (element.Y, element.Height)),
            out double? horizontalGuide);
        return new HardwareEditorSnapResult(snappedLeft, snappedTop, verticalGuide, horizontalGuide);
    }

    private static double SnapHardwareEditorAxis(
        double position,
        double size,
        double maxPosition,
        IEnumerable<(double Position, double Size)> targets,
        out double? guidePosition)
    {
        double snappedPosition = Math.Clamp(position, 0, maxPosition);
        double bestDistance = HardwareEditorSnapThreshold + 1;
        guidePosition = null;
        foreach ((double targetPosition, double targetSize) in targets)
        {
            foreach (double targetGuide in GetHardwareEditorAxisGuides(targetPosition, targetSize))
            {
                foreach (double offset in GetHardwareEditorAxisOffsets(size))
                {
                    double candidatePosition = targetGuide - offset;
                    if (candidatePosition < 0 || candidatePosition > maxPosition)
                    {
                        continue;
                    }

                    double distance = Math.Abs(candidatePosition - position);
                    if (distance <= HardwareEditorSnapThreshold && distance < bestDistance)
                    {
                        snappedPosition = candidatePosition;
                        guidePosition = targetGuide;
                        bestDistance = distance;
                    }
                }
            }
        }

        return snappedPosition;
    }

    private static IEnumerable<double> GetHardwareEditorAxisGuides(double position, double size)
    {
        yield return position;
        yield return position + (size / 2);
        yield return position + size;
    }

    private static IEnumerable<double> GetHardwareEditorAxisOffsets(double size)
    {
        yield return 0;
        yield return size / 2;
        yield return size;
    }

    private static void UpdateHardwareEditorGuides(
        Line verticalGuide,
        Line horizontalGuide,
        double? verticalPosition,
        double? horizontalPosition,
        double canvasWidth,
        double canvasHeight)
    {
        if (verticalPosition.HasValue)
        {
            verticalGuide.X1 = verticalPosition.Value;
            verticalGuide.X2 = verticalPosition.Value;
            verticalGuide.Y1 = 0;
            verticalGuide.Y2 = canvasHeight;
            verticalGuide.Visibility = Visibility.Visible;
        }
        else
        {
            verticalGuide.Visibility = Visibility.Collapsed;
        }

        if (horizontalPosition.HasValue)
        {
            horizontalGuide.X1 = 0;
            horizontalGuide.X2 = canvasWidth;
            horizontalGuide.Y1 = horizontalPosition.Value;
            horizontalGuide.Y2 = horizontalPosition.Value;
            horizontalGuide.Visibility = Visibility.Visible;
        }
        else
        {
            horizontalGuide.Visibility = Visibility.Collapsed;
        }
    }

    private static void HideHardwareEditorGuides(Line verticalGuide, Line horizontalGuide)
    {
        verticalGuide.Visibility = Visibility.Collapsed;
        horizontalGuide.Visibility = Visibility.Collapsed;
    }

    private static bool TryCreateSettingsBitmapImage(string path, out BitmapImage? bitmap)
    {
        bitmap = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            bitmap = new BitmapImage(new Uri(path));
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            bitmap = null;
            return false;
        }
    }

    private static SolidColorBrush CreateSettingsColorBrush(string value)
    {
        return new SolidColorBrush(ParseSettingsColor(value));
    }

    private static global::Windows.UI.Color ParseSettingsColor(string value)
    {
        string hex = value.Trim().TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (hex.Length == 8 && uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint argb))
        {
            return Microsoft.UI.ColorHelper.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }

        return Microsoft.UI.Colors.White;
    }

    private static string FormatSettingsColor(global::Windows.UI.Color color)
    {
        return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
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

        config.SelectedSensorIds = HardwareOverlayTextRenderer.SelectDefaultSensors(snapshot)
            .Take(8)
            .Select(sensor => sensor.Id)
            .ToList();
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
