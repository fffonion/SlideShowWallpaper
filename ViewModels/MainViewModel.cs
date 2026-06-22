using System.Collections.ObjectModel;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.ViewModels;

public sealed class MainViewModel
{
    public ObservableCollection<MonitorSettingsViewModel> Monitors { get; } = [];

    public bool StartWithWindows { get; set; }

    public bool CloseToTray { get; set; } = true;

    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.System;

    public bool PlaybackEnabled { get; set; } = true;

    public IReadOnlyList<MonitorProfile> Profiles => Monitors.Select(monitor => monitor.Profile).ToArray();
}
