using System.Collections.ObjectModel;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.ViewModels;

public sealed class MainViewModel
{
    public ObservableCollection<MonitorSettingsViewModel> Monitors { get; } = [];

    public bool StartWithWindows { get; set; }

    public bool StartWithWindowsAsAdministrator { get; set; }

    public bool CloseToTray { get; set; } = true;

    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.System;

    public AppLanguageMode LanguageMode { get; set; } = AppLanguageMode.System;

    public bool PlaybackEnabled { get; set; } = true;

    public bool AutoTrackNewFiles { get; set; } = true;

    public bool GlobalMute { get; set; } = true;

    public bool ThumbnailCacheEnabled { get; set; } = true;

    public bool PauseVideoWhenDisplayOffOrSleeping { get; set; } = true;

    public int PreviewPopupDelaySeconds { get; set; } = WallpaperConfig.DefaultPreviewPopupDelaySeconds;

    public HardwareMonitorConfig HardwareMonitor { get; set; } = new();

    public IReadOnlyList<MonitorProfile> Profiles => Monitors.Select(monitor => monitor.Profile).ToArray();
}
