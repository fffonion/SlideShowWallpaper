using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using SlideShowWallpaper.ViewModels;

namespace SlideShowWallpaper;

public sealed partial class MainWindow
{
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
        SettingsNavigationPanel.Children.Add(CreateHardwareMonitorNavigationItem());
        SettingsNavigationPanel.Children.Add(CreateSettingsNavigationItem());

        if (_isHardwareMonitorSelected || _isSettingsSelected)
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

    private Button CreateHardwareMonitorNavigationItem()
    {
        string label = LocalizedStrings.Get("HardwareMonitorSettingsGroup");
        return CreateNavigationItem(label, "\uE950", HardwareMonitorNavigationTag, HardwareMonitorNavigationItem_Click);
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
        AutomationProperties.SetAutomationId(item, tag switch
        {
            MonitorProfile profile => $"MonitorNavigationItem_{profile.Id}",
            string value when string.Equals(value, HardwareMonitorNavigationTag, StringComparison.Ordinal) => "HardwareMonitorNavigationItem",
            _ => "SettingsNavigationItem",
        });
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

            bool isSelected = !_isHardwareMonitorSelected && !_isSettingsSelected && string.Equals(profile.Id, _selectedMonitorId, StringComparison.OrdinalIgnoreCase);
            UpdateNavigationButtonVisual(item, visuals, isSelected);
        }

        foreach (UIElement element in SettingsNavigationPanel.Children)
        {
            if (element is not Button { Tag: string tag, Content: Border { Tag: MonitorNavigationVisuals visuals } } item)
            {
                continue;
            }

            bool isSelected = string.Equals(tag, HardwareMonitorNavigationTag, StringComparison.Ordinal)
                ? _isHardwareMonitorSelected
                : _isSettingsSelected && string.Equals(tag, SettingsNavigationTag, StringComparison.Ordinal);
            UpdateNavigationButtonVisual(item, visuals, isSelected);
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
        if (_isHardwareMonitorSelected)
        {
            MonitorContent.Content = BuildHardwareMonitorSettingsPage();
            return;
        }

        if (_isSettingsSelected)
        {
            MonitorContent.Content = BuildAppSettingsPage();
            return;
        }

        MonitorContent.Content = SelectedProfile is { } profile ? BuildMonitorPage(profile) : null;
    }

    private void MonitorNavigationItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MonitorProfile profile })
        {
            CancelSelectedPreviewLoad();
            _selectedMonitorId = profile.Id;
            _isHardwareMonitorSelected = false;
            _isSettingsSelected = false;
        }

        UpdateMonitorNavigationVisuals();
        ShowSelectedMonitorPage();
    }

    private void SettingsNavigationItem_Click(object sender, RoutedEventArgs e)
    {
        CancelSelectedPreviewLoad();
        _selectedMonitorId = null;
        _isHardwareMonitorSelected = false;
        _isSettingsSelected = true;
        UpdateMonitorNavigationVisuals();
        ShowSelectedMonitorPage();
    }

    private void HardwareMonitorNavigationItem_Click(object sender, RoutedEventArgs e)
    {
        CancelSelectedPreviewLoad();
        _selectedMonitorId = null;
        _isHardwareMonitorSelected = true;
        _isSettingsSelected = false;
        UpdateMonitorNavigationVisuals();
        ShowSelectedMonitorPage();
    }
}
