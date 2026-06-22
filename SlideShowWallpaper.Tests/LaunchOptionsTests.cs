using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class LaunchOptionsTests
{
    [Fact]
    public void FromArguments_WithQuietSwitch_StartsInTray()
    {
        LaunchOptions options = LaunchOptions.FromArguments(["/q"]);

        Assert.True(options.StartInTray);
    }

    [Fact]
    public void FromArguments_WithoutQuietSwitch_ShowsSettingsWindow()
    {
        LaunchOptions options = LaunchOptions.FromArguments([]);

        Assert.False(options.StartInTray);
    }
}
