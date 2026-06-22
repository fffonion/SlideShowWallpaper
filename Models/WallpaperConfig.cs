namespace SlideShowWallpaper.Models;

public enum WallpaperScaleMode
{
    Fit,
    Cover,
    Stretch,
    Original
}

public enum WallpaperAlignment
{
    Center
}

public enum PlaybackOrder
{
    SequentialLoop,
    Random,
    SingleLoop,
    NameAsc,
    NameDesc,
    ModifiedDateAsc,
    ModifiedDateDesc
}

public enum TimeUnit
{
    Seconds,
    Minutes,
    Hours
}

public enum WallpaperTransition
{
    None,
    Fade,
    Slide
}

public enum AppThemeMode
{
    System,
    Light,
    Dark
}

public sealed class WallpaperConfig
{
    public bool StartWithWindows { get; set; }

    public bool CloseToTray { get; set; } = true;

    public AppThemeMode ThemeMode { get; set; } = AppThemeMode.System;

    public bool PlaybackEnabled { get; set; } = true;

    public List<MonitorProfile> Monitors { get; set; } = [];
}

public static class TimeUnitConverter
{
    public static double ToSeconds(double value, TimeUnit unit)
    {
        return unit switch
        {
            TimeUnit.Hours => value * 60 * 60,
            TimeUnit.Minutes => value * 60,
            _ => value,
        };
    }

    public static int ToMilliseconds(double value, TimeUnit unit)
    {
        return (int)Math.Round(ToSeconds(value, unit) * 1000);
    }
}
