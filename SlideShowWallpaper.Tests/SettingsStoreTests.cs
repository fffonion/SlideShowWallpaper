using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void SettingsPath_WithDefaultConstructor_UsesApplicationDirectoryIni()
    {
        var store = new SettingsStore();

        Assert.Equal("SlideShowWallpaper.ini", Path.GetFileName(store.SettingsPath));
        string expectedFolder = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? AppContext.BaseDirectory;
        Assert.Equal(
            expectedFolder.TrimEnd(Path.DirectorySeparatorChar),
            Path.GetDirectoryName(store.SettingsPath)?.TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void SaveAndLoad_WithMonitorProfiles_RoundTripsIniFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.ini");
        var store = new SettingsStore(path);
        var config = new WallpaperConfig
        {
            StartWithWindows = true,
            StartWithWindowsAsAdministrator = true,
            CloseToTray = false,
            ThemeMode = AppThemeMode.Dark,
            LanguageMode = AppLanguageMode.Japanese,
            PlaybackEnabled = true,
            AutoTrackNewFiles = false,
            GlobalMute = false,
            ThumbnailCacheEnabled = false,
            PauseVideoWhenDisplayOffOrSleeping = false,
            PreviewPopupDelaySeconds = 3,
            HardwareMonitor = new HardwareMonitorConfig
            {
                IsEnabled = true,
                RefreshIntervalSeconds = 9,
                TargetMonitorId = "display1",
                TemplateText = "Hardware{metrics}",
                X = 32,
                Y = 48,
                FontFamily = "Cascadia Mono",
                FontSize = 18,
                Opacity = 0.72,
                SelectedSensorIds = ["cpu-temp", "gpu-power"],
                BackgroundImagePath = @"C:\Wallpapers\panel.png",
                SelectedElementId = "element1",
                Elements =
                [
                    new HardwareOverlayElement
                    {
                        Id = "element1",
                        Kind = HardwareOverlayElementKind.Sensor,
                        SensorId = "cpu-temp",
                        Text = "CPU",
                        ImagePath = @"C:\Wallpapers\cpu.png",
                        X = 12,
                        Y = 34,
                        Width = 144,
                        Height = 36,
                        FontFamily = "Segoe UI Variable",
                        FontSize = 17,
                        Foreground = "#FF00FFFF",
                        Opacity = 0.8,
                    },
                ],
            },
            Monitors =
            [
                new MonitorProfile
                {
                    Id = "display1",
                    DisplayName = "Dell U2723QE",
                    FolderPath = @"C:\Wallpapers",
                    ScaleMode = WallpaperScaleMode.Stretch,
                    OffsetX = 12.5,
                    OffsetY = -4,
                    PlaybackOrder = PlaybackOrder.ModifiedDateDesc,
                    IntervalSeconds = 120,
                    IntervalUnit = TimeUnit.Minutes,
                    Transition = WallpaperTransition.Slide,
                    TransitionDurationMs = 2400,
                    TransitionDurationUnit = TimeUnit.Seconds,
                    VideoLoop = true,
                    VideoSoundEnabled = true,
                    PauseVideoWhenOtherAppMaximized = true,
                    MediaFilter = PlaybackMediaFilter.ImagesOnly,
                    IsPaused = true,
                    IsStopped = true,
                    SelectedImagePath = @"C:\Wallpapers\a.png",
                },
            ],
        };

        store.Save(config);
        WallpaperConfig loaded = store.Load();

        Assert.True(File.Exists(path));
        Assert.Contains("[Settings]", File.ReadAllText(path));
        MonitorProfile monitor = Assert.Single(loaded.Monitors);
        Assert.True(loaded.StartWithWindows);
        Assert.True(loaded.StartWithWindowsAsAdministrator);
        Assert.False(loaded.CloseToTray);
        Assert.Equal(AppThemeMode.Dark, loaded.ThemeMode);
        Assert.Equal(AppLanguageMode.Japanese, loaded.LanguageMode);
        Assert.False(loaded.AutoTrackNewFiles);
        Assert.False(loaded.GlobalMute);
        Assert.False(loaded.ThumbnailCacheEnabled);
        Assert.False(loaded.PauseVideoWhenDisplayOffOrSleeping);
        Assert.Equal(3, loaded.PreviewPopupDelaySeconds);
        Assert.True(loaded.HardwareMonitor.IsEnabled);
        Assert.Equal(9, loaded.HardwareMonitor.RefreshIntervalSeconds);
        Assert.Equal("display1", loaded.HardwareMonitor.TargetMonitorId);
        Assert.Equal("Hardware{metrics}", loaded.HardwareMonitor.TemplateText);
        Assert.Equal(32, loaded.HardwareMonitor.X);
        Assert.Equal(48, loaded.HardwareMonitor.Y);
        Assert.Equal("Cascadia Mono", loaded.HardwareMonitor.FontFamily);
        Assert.Equal(18, loaded.HardwareMonitor.FontSize);
        Assert.Equal(0.72, loaded.HardwareMonitor.Opacity);
        Assert.Equal(["cpu-temp", "gpu-power"], loaded.HardwareMonitor.SelectedSensorIds);
        Assert.Equal(@"C:\Wallpapers\panel.png", loaded.HardwareMonitor.BackgroundImagePath);
        Assert.Equal("element1", loaded.HardwareMonitor.SelectedElementId);
        HardwareOverlayElement element = Assert.Single(loaded.HardwareMonitor.Elements);
        Assert.Equal(HardwareOverlayElementKind.Sensor, element.Kind);
        Assert.Equal("cpu-temp", element.SensorId);
        Assert.Equal("CPU", element.Text);
        Assert.Equal(@"C:\Wallpapers\cpu.png", element.ImagePath);
        Assert.Equal(12, element.X);
        Assert.Equal(34, element.Y);
        Assert.Equal(144, element.Width);
        Assert.Equal(36, element.Height);
        Assert.Equal("Segoe UI Variable", element.FontFamily);
        Assert.Equal(17, element.FontSize);
        Assert.Equal("#FF00FFFF", element.Foreground);
        Assert.Equal(0.8, element.Opacity);
        Assert.Equal("display1", monitor.Id);
        Assert.Equal("Dell U2723QE", monitor.DisplayName);
        Assert.Equal(PlaybackOrder.ModifiedDateDesc, monitor.PlaybackOrder);
        Assert.Equal(TimeUnit.Minutes, monitor.IntervalUnit);
        Assert.True(monitor.VideoLoop);
        Assert.True(monitor.VideoSoundEnabled);
        Assert.True(monitor.PauseVideoWhenOtherAppMaximized);
        Assert.Equal(PlaybackMediaFilter.ImagesOnly, monitor.MediaFilter);
        Assert.True(monitor.IsStopped);
    }

    [Fact]
    public void Load_WithMissingScaleMode_UsesCover()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.ini");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            path,
            """
            [Settings]
            MonitorCount=1

            [Monitor0]
            Id=display1
            DisplayName=Display 1
            """);
        var store = new SettingsStore(path);

        WallpaperConfig config = store.Load();

        MonitorProfile monitor = Assert.Single(config.Monitors);
        Assert.Equal(WallpaperScaleMode.Cover, monitor.ScaleMode);
    }

    [Fact]
    public void MonitorProfile_WithDefaultConstructor_UsesCoverScaleMode()
    {
        var profile = new MonitorProfile();

        Assert.Equal(WallpaperScaleMode.Cover, profile.ScaleMode);
    }

    [Fact]
    public void Load_WithMissingPlaybackOrder_UsesRandom()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.ini");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            path,
            """
            [Settings]
            MonitorCount=1

            [Monitor0]
            Id=display1
            DisplayName=Display 1
            """);
        var store = new SettingsStore(path);

        WallpaperConfig config = store.Load();

        MonitorProfile monitor = Assert.Single(config.Monitors);
        Assert.Equal(PlaybackOrder.Random, monitor.PlaybackOrder);
    }

    [Fact]
    public void MonitorProfile_WithDefaultConstructor_UsesRandomPlaybackOrder()
    {
        var profile = new MonitorProfile();

        Assert.Equal(PlaybackOrder.Random, profile.PlaybackOrder);
    }

    [Fact]
    public void MonitorProfile_WithDefaultConstructor_EnablesVideoLoop()
    {
        var profile = new MonitorProfile();

        Assert.True(profile.VideoLoop);
    }

    [Fact]
    public void Load_WithMissingVideoLoop_UsesTrue()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.ini");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            path,
            """
            [Settings]
            MonitorCount=1

            [Monitor0]
            Id=display1
            DisplayName=Display 1
            """);
        var store = new SettingsStore(path);

        WallpaperConfig config = store.Load();

        MonitorProfile monitor = Assert.Single(config.Monitors);
        Assert.True(monitor.VideoLoop);
    }

    [Fact]
    public void MonitorProfile_WithDefaultConstructor_DisablesVideoSound()
    {
        var profile = new MonitorProfile();

        Assert.False(profile.VideoSoundEnabled);
    }

    [Fact]
    public void MonitorProfile_WithDefaultConstructor_PausesVideoWhenOtherAppMaximized()
    {
        var profile = new MonitorProfile();

        Assert.True(profile.PauseVideoWhenOtherAppMaximized);
    }

    [Fact]
    public void WallpaperConfig_WithDefaultConstructor_PausesVideoWhenDisplayOffOrSleeping()
    {
        var config = new WallpaperConfig();

        Assert.True(config.PauseVideoWhenDisplayOffOrSleeping);
    }

    [Fact]
    public void Load_WithMissingPauseVideoWhenDisplayOffOrSleeping_UsesTrue()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.ini");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            path,
            """
            [Settings]
            MonitorCount=0
            """);
        var store = new SettingsStore(path);

        WallpaperConfig config = store.Load();

        Assert.True(config.PauseVideoWhenDisplayOffOrSleeping);
    }

    [Fact]
    public void Load_WithMissingPauseVideoWhenOtherAppMaximized_UsesTrue()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.ini");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            path,
            """
            [Settings]
            MonitorCount=1

            [Monitor0]
            Id=display1
            DisplayName=Display 1
            """);
        var store = new SettingsStore(path);

        WallpaperConfig config = store.Load();

        MonitorProfile monitor = Assert.Single(config.Monitors);
        Assert.True(monitor.PauseVideoWhenOtherAppMaximized);
    }

    [Fact]
    public void Load_WithMissingCloseToTray_UsesTrue()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.ini");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            path,
            """
            [Settings]
            MonitorCount=0
            """);
        var store = new SettingsStore(path);

        WallpaperConfig config = store.Load();

        Assert.True(config.CloseToTray);
    }

    [Fact]
    public void Load_WithMissingThemeMode_UsesSystem()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.ini");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            path,
            """
            [Settings]
            MonitorCount=0
            """);
        var store = new SettingsStore(path);

        WallpaperConfig config = store.Load();

        Assert.Equal(AppThemeMode.System, config.ThemeMode);
    }

    [Fact]
    public void Load_WithMissingPreviewPopupDelay_UsesTwoSeconds()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.ini");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            path,
            """
            [Settings]
            MonitorCount=0
            """);
        var store = new SettingsStore(path);

        WallpaperConfig config = store.Load();

        Assert.Equal(WallpaperConfig.DefaultPreviewPopupDelaySeconds, config.PreviewPopupDelaySeconds);
    }

    [Fact]
    public void HardwareMonitorConfig_WithDefaultConstructor_UsesFiveSecondRefreshInterval()
    {
        var config = new HardwareMonitorConfig();

        Assert.Equal(5, config.RefreshIntervalSeconds);
    }

    [Fact]
    public void Load_WithMissingHardwareMonitorRefreshInterval_UsesFiveSeconds()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.ini");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            path,
            """
            [Settings]
            MonitorCount=0

            [HardwareMonitor]
            IsEnabled=True
            """);
        var store = new SettingsStore(path);

        WallpaperConfig config = store.Load();

        Assert.Equal(HardwareMonitorConfig.DefaultRefreshIntervalSeconds, config.HardwareMonitor.RefreshIntervalSeconds);
    }
}
