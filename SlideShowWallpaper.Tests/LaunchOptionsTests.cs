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

    [Fact]
    public void FromArguments_WithMultipleSwitch_AllowsMultipleInstances()
    {
        LaunchOptions options = LaunchOptions.FromArguments(["/multiple"]);

        Assert.True(options.AllowMultipleInstances);
    }

    [Fact]
    public void FromArguments_WithMultipleSwitch_DisablesCloseToTray()
    {
        LaunchOptions options = LaunchOptions.FromArguments(["/multiple"]);

        Assert.True(options.DisableCloseToTray);
    }

    [Fact]
    public void FromArguments_WithElevatedRestartSwitch_AllowsMultipleWithoutDisablingCloseToTray()
    {
        LaunchOptions options = LaunchOptions.FromArguments([AdministratorRestartService.RestartArgument]);

        Assert.True(options.AllowMultipleInstances);
        Assert.False(options.DisableCloseToTray);
    }
}
