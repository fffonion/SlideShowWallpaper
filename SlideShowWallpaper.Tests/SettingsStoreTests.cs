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
            CloseToTray = false,
            ThemeMode = AppThemeMode.Dark,
            PlaybackEnabled = true,
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
        Assert.False(loaded.CloseToTray);
        Assert.Equal(AppThemeMode.Dark, loaded.ThemeMode);
        Assert.Equal("display1", monitor.Id);
        Assert.Equal("Dell U2723QE", monitor.DisplayName);
        Assert.Equal(PlaybackOrder.ModifiedDateDesc, monitor.PlaybackOrder);
        Assert.Equal(TimeUnit.Minutes, monitor.IntervalUnit);
        Assert.True(monitor.VideoLoop);
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
}
