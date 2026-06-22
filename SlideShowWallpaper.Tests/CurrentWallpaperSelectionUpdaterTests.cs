using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class CurrentWallpaperSelectionUpdaterTests
{
    [Fact]
    public void Update_WithCurrentWallpaperPath_UpdatesMatchingMonitor()
    {
        var first = new MonitorProfile { Id = "display1", SelectedImagePath = string.Empty };
        var second = new MonitorProfile { Id = "display2", SelectedImagePath = string.Empty };

        bool updated = CurrentWallpaperSelectionUpdater.Update([first, second], "DISPLAY2", @"C:\Wallpapers\current.jpg");

        Assert.True(updated);
        Assert.Equal(string.Empty, first.SelectedImagePath);
        Assert.Equal(@"C:\Wallpapers\current.jpg", second.SelectedImagePath);
    }

    [Fact]
    public void Update_WithEmptyPath_DoesNotOverwriteSavedSelection()
    {
        var profile = new MonitorProfile { Id = "display1", SelectedImagePath = @"C:\Wallpapers\old.jpg" };

        bool updated = CurrentWallpaperSelectionUpdater.Update([profile], "display1", string.Empty);

        Assert.False(updated);
        Assert.Equal(@"C:\Wallpapers\old.jpg", profile.SelectedImagePath);
    }
}
