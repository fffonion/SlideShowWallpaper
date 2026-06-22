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
            new SettingsRow(LocalizedStrings.Get("AppSettingThumbnailCache"), CreateCheckBox(_viewModel.ThumbnailCacheEnabled, SetThumbnailCacheEnabled, LocalizedStrings.Get("AppSettingThumbnailCache"))),
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
        return root;
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