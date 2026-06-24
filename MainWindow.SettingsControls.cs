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

    private Grid CreateThumbnailCacheControls()
    {
        var panel = new Grid
        {
            ColumnSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        CheckBox cacheCheckBox = CreateCheckBox(_viewModel.ThumbnailCacheEnabled, SetThumbnailCacheEnabled, LocalizedStrings.Get("AppSettingThumbnailCache"));
        Grid.SetColumn(cacheCheckBox, 0);
        panel.Children.Add(cacheCheckBox);

        var statusHost = new Grid
        {
            MinWidth = 96,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _thumbnailCacheSizeProgress = new ProgressRing
        {
            Width = 18,
            Height = 18,
            IsActive = true,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        AutomationProperties.SetName(_thumbnailCacheSizeProgress, LocalizedStrings.Get("ThumbnailCacheSizeLoading"));
        statusHost.Children.Add(_thumbnailCacheSizeProgress);

        _thumbnailCacheSizeText = new TextBlock
        {
            Text = string.Empty,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetName(_thumbnailCacheSizeText, LocalizedStrings.Get("ThumbnailCacheSize"));
        statusHost.Children.Add(_thumbnailCacheSizeText);
        Grid.SetColumn(statusHost, 1);
        panel.Children.Add(statusHost);

        _clearThumbnailCacheButton = new Button
        {
            Content = new SymbolIcon(Symbol.Delete),
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
        };
        AutomationProperties.SetName(_clearThumbnailCacheButton, LocalizedStrings.Get("ClearThumbnailCache"));
        ToolTipService.SetToolTip(_clearThumbnailCacheButton, LocalizedStrings.Get("ClearThumbnailCache"));
        _clearThumbnailCacheButton.Click += async (_, _) => await ClearThumbnailCacheAsync();
        Grid.SetColumn(_clearThumbnailCacheButton, 2);
        panel.Children.Add(_clearThumbnailCacheButton);

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

    private static Border CreateSettingsContentSection(string title, FrameworkElement content)
    {
        var grid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(16, 14, 16, 10),
        };
        AutomationProperties.SetName(titleBlock, title);
        Grid.SetRow(titleBlock, 0);
        grid.Children.Add(titleBlock);

        Border divider = CreateSettingsDivider();
        Grid.SetRow(divider, 1);
        grid.Children.Add(divider);

        content.Margin = new Thickness(16, 12, 16, 14);
        var scrollViewer = new ScrollViewer
        {
            Content = content,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetRow(scrollViewer, 2);
        grid.Children.Add(scrollViewer);

        return new Border
        {
            Background = GetThemeBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = grid,
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

        if (row.IsFullWidth)
        {
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddSettingsRowControl(content, row.Control, column: 0);

            return new Border
            {
                Child = content,
            };
        }

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

        AddSettingsRowControl(content, row.Control, column: 1);

        return new Border
        {
            Child = content,
        };
    }

    private static void AddSettingsRowControl(Grid content, FrameworkElement control, int column)
    {
        control.VerticalAlignment = VerticalAlignment.Center;
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(control, column);
        content.Children.Add(control);
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

    private Grid CreateOffsetControls(MonitorProfile profile)
    {
        var panel = new Grid
        {
            ColumnSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        FrameworkElement xControl = CreateLabeledNumberBox("X", profile.OffsetX, value => profile.OffsetX = value);
        Grid.SetColumn(xControl, 0);
        panel.Children.Add(xControl);

        FrameworkElement yControl = CreateLabeledNumberBox("Y", profile.OffsetY, value => profile.OffsetY = value);
        Grid.SetColumn(yControl, 1);
        panel.Children.Add(yControl);
        return panel;
    }

    private Grid CreateLabeledNumberBox(string label, double value, Action<double> changed)
    {
        var panel = new Grid
        {
            ColumnSpacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelBlock = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(labelBlock, 0);
        panel.Children.Add(labelBlock);

        NumberBox numberBox = CreateNumberBox(value, changed, label);
        Grid.SetColumn(numberBox, 1);
        panel.Children.Add(numberBox);
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
            MinWidth = 160,
            HorizontalAlignment = HorizontalAlignment.Stretch,
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
            MinWidth = 72,
            HorizontalAlignment = HorizontalAlignment.Stretch,
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

    private Grid CreateOpacitySlider(double value, Action<double> changed, string automationName)
    {
        var panel = new Grid
        {
            ColumnSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var slider = new Slider
        {
            Minimum = 10,
            Maximum = 100,
            StepFrequency = 1,
            SmallChange = 1,
            LargeChange = 10,
            Value = Math.Clamp(value, 0.1, 1) * 100,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(slider, automationName);

        var valueText = new TextBlock
        {
            MinWidth = 42,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(valueText, automationName);

        void UpdateText(double sliderValue)
        {
            double percentage = Math.Clamp(sliderValue, 10, 100);
            valueText.Text = $"{percentage:0}%";
        }

        UpdateText(slider.Value);
        slider.ValueChanged += (_, args) =>
        {
            double percentage = Math.Clamp(args.NewValue, 10, 100);
            valueText.Text = $"{percentage:0}%";
            changed(percentage / 100);
            ScheduleApplySettings();
        };

        Grid.SetColumn(slider, 0);
        panel.Children.Add(slider);
        Grid.SetColumn(valueText, 1);
        panel.Children.Add(valueText);
        return panel;
    }

    private Grid CreateTimedNumberBox(double value, TimeUnit unit, Action<double, TimeUnit> changed, string automationName, bool isEnabled = true)
    {
        var panel = new Grid
        {
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
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
        valueBox.IsEnabled = isEnabled;
        unitCombo.IsEnabled = isEnabled;

        Grid.SetColumn(valueBox, 0);
        panel.Children.Add(valueBox);
        Grid.SetColumn(unitCombo, 1);
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
}
