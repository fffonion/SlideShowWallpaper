namespace SlideShowWallpaper.Models;

public sealed class MonitorProfile
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string FolderPath { get; set; } = string.Empty;

    public WallpaperScaleMode ScaleMode { get; set; } = WallpaperScaleMode.Cover;

    public WallpaperAlignment Alignment { get; set; } = WallpaperAlignment.Center;

    public double OffsetX { get; set; }

    public double OffsetY { get; set; }

    public PlaybackOrder PlaybackOrder { get; set; } = PlaybackOrder.Random;

    public int IntervalSeconds { get; set; } = 60;

    public TimeUnit IntervalUnit { get; set; } = TimeUnit.Seconds;

    public WallpaperTransition Transition { get; set; } = WallpaperTransition.Fade;

    public int TransitionDurationMs { get; set; } = 800;

    public TimeUnit TransitionDurationUnit { get; set; } = TimeUnit.Seconds;

    public bool VideoLoop { get; set; } = true;

    public bool VideoSoundEnabled { get; set; }

    public PlaybackMediaFilter MediaFilter { get; set; } = PlaybackMediaFilter.ImagesAndVideos;

    public bool IsPaused { get; set; }

    public bool IsStopped { get; set; }

    public string SelectedImagePath { get; set; } = string.Empty;

    public int CurrentMediaIndex { get; set; }

    public int TotalMediaCount { get; set; }

    public DateTimeOffset? CurrentMediaStartedAt { get; set; }
}
