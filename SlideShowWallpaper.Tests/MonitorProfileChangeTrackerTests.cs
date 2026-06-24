using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class MonitorProfileChangeTrackerTests
{
    [Fact]
    public void Update_WithNewProfile_RequiresQueueRefreshOnly()
    {
        var tracker = new MonitorProfileChangeTracker();
        var profile = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers",
            PlaybackOrder = PlaybackOrder.NameAsc,
        };

        MonitorProfileChange change = tracker.Update(profile);

        Assert.True(change.QueueChanged);
        Assert.False(change.VisualChanged);
    }

    [Fact]
    public void Update_WithScaleChange_RequiresVisualRefreshOnly()
    {
        var tracker = new MonitorProfileChangeTracker();
        var profile = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers",
            PlaybackOrder = PlaybackOrder.NameAsc,
            ScaleMode = WallpaperScaleMode.Cover,
        };
        _ = tracker.Update(profile);

        profile.ScaleMode = WallpaperScaleMode.Stretch;
        MonitorProfileChange change = tracker.Update(profile);

        Assert.False(change.QueueChanged);
        Assert.True(change.VisualChanged);
        Assert.True(change.HasChanges);
    }

    [Fact]
    public void Update_WithFolderChange_RequiresQueueRefresh()
    {
        var tracker = new MonitorProfileChangeTracker();
        var profile = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers",
            PlaybackOrder = PlaybackOrder.NameAsc,
        };
        _ = tracker.Update(profile);

        profile.FolderPath = @"D:\Wallpapers";
        MonitorProfileChange change = tracker.Update(profile);

        Assert.True(change.QueueChanged);
        Assert.False(change.VisualChanged);
        Assert.True(change.HasChanges);
    }

    [Fact]
    public void Update_WithUnchangedProfile_ReturnsNoChanges()
    {
        var tracker = new MonitorProfileChangeTracker();
        var profile = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers",
            PlaybackOrder = PlaybackOrder.NameAsc,
        };
        _ = tracker.Update(profile);

        MonitorProfileChange change = tracker.Update(profile);

        Assert.False(change.QueueChanged);
        Assert.False(change.VisualChanged);
        Assert.False(change.PlaybackSettingsChanged);
        Assert.False(change.HasChanges);
    }

    [Fact]
    public void Update_WithOnlyFirstProfileChanged_SecondProfileReturnsNoChanges()
    {
        var tracker = new MonitorProfileChangeTracker();
        var first = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers\A",
            PlaybackOrder = PlaybackOrder.NameAsc,
        };
        var second = new MonitorProfile
        {
            Id = "display2",
            FolderPath = @"C:\Wallpapers\B",
            PlaybackOrder = PlaybackOrder.NameAsc,
        };
        _ = tracker.Update(first);
        _ = tracker.Update(second);

        first.OffsetX = 24;
        MonitorProfileChange firstChange = tracker.Update(first);
        MonitorProfileChange secondChange = tracker.Update(second);

        Assert.True(firstChange.VisualChanged);
        Assert.True(firstChange.HasChanges);
        Assert.False(secondChange.HasChanges);
    }

    [Fact]
    public void Update_WithIntervalChange_RequiresPlaybackSettingsRefreshOnly()
    {
        var tracker = new MonitorProfileChangeTracker();
        var profile = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers",
            PlaybackOrder = PlaybackOrder.NameAsc,
            IntervalSeconds = 60,
        };
        _ = tracker.Update(profile);

        profile.IntervalSeconds = 120;
        MonitorProfileChange change = tracker.Update(profile);

        Assert.False(change.QueueChanged);
        Assert.False(change.VisualChanged);
        Assert.True(change.PlaybackSettingsChanged);
        Assert.True(change.HasChanges);
    }

    [Fact]
    public void Update_WithVideoLoopChange_RequiresPlaybackSettingsRefreshOnly()
    {
        var tracker = new MonitorProfileChangeTracker();
        var profile = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers",
            PlaybackOrder = PlaybackOrder.NameAsc,
            VideoLoop = false,
        };
        _ = tracker.Update(profile);

        profile.VideoLoop = true;
        MonitorProfileChange change = tracker.Update(profile);

        Assert.False(change.QueueChanged);
        Assert.False(change.VisualChanged);
        Assert.True(change.PlaybackSettingsChanged);
        Assert.True(change.HasChanges);
    }

    [Fact]
    public void Update_WithVideoSoundChange_RequiresPlaybackSettingsRefreshOnly()
    {
        var tracker = new MonitorProfileChangeTracker();
        var profile = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers",
            PlaybackOrder = PlaybackOrder.NameAsc,
            VideoSoundEnabled = false,
        };
        _ = tracker.Update(profile);

        profile.VideoSoundEnabled = true;
        MonitorProfileChange change = tracker.Update(profile);

        Assert.False(change.QueueChanged);
        Assert.False(change.VisualChanged);
        Assert.True(change.PlaybackSettingsChanged);
        Assert.True(change.HasChanges);
    }

    [Fact]
    public void Update_WithPauseVideoCoverageChange_RequiresPlaybackSettingsRefreshOnly()
    {
        var tracker = new MonitorProfileChangeTracker();
        var profile = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers",
            PlaybackOrder = PlaybackOrder.NameAsc,
            PauseVideoWhenOtherAppMaximized = true,
        };
        _ = tracker.Update(profile);

        profile.PauseVideoWhenOtherAppMaximized = false;
        MonitorProfileChange change = tracker.Update(profile);

        Assert.False(change.QueueChanged);
        Assert.False(change.VisualChanged);
        Assert.True(change.PlaybackSettingsChanged);
        Assert.True(change.HasChanges);
    }

    [Fact]
    public void Update_WithMediaFilterChange_RequiresQueueRefresh()
    {
        var tracker = new MonitorProfileChangeTracker();
        var profile = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers",
            PlaybackOrder = PlaybackOrder.NameAsc,
            MediaFilter = PlaybackMediaFilter.ImagesAndVideos,
        };
        _ = tracker.Update(profile);

        profile.MediaFilter = PlaybackMediaFilter.ImagesOnly;
        MonitorProfileChange change = tracker.Update(profile);

        Assert.True(change.QueueChanged);
        Assert.False(change.VisualChanged);
        Assert.True(change.HasChanges);
    }

    [Fact]
    public void Update_WithRecursiveSubdirectoryChange_RequiresQueueRefresh()
    {
        var tracker = new MonitorProfileChangeTracker();
        var profile = new MonitorProfile
        {
            Id = "display1",
            FolderPath = @"C:\Wallpapers",
            PlaybackOrder = PlaybackOrder.NameAsc,
            IncludeSubdirectories = false,
        };
        _ = tracker.Update(profile);

        profile.IncludeSubdirectories = true;
        MonitorProfileChange change = tracker.Update(profile);

        Assert.True(change.QueueChanged);
        Assert.False(change.VisualChanged);
        Assert.True(change.HasChanges);
    }
}
