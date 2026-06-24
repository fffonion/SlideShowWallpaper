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
    public void FromArguments_WithMultipleSwitch_DoesNotSkipElevationDemotion()
    {
        LaunchOptions options = LaunchOptions.FromArguments(["/multiple"]);

        Assert.False(options.SkipElevationDemotion);
    }

    [Fact]
    public void FromArguments_WithElevatedRestartSwitch_AllowsMultipleWithoutDisablingCloseToTray()
    {
        LaunchOptions options = LaunchOptions.FromArguments([AdministratorRestartService.RestartArgument]);

        Assert.True(options.AllowMultipleInstances);
        Assert.False(options.DisableCloseToTray);
        Assert.False(options.SkipElevationDemotion);
    }

    [Fact]
    public void FromArguments_WithElevatedBrokerSwitch_StartsBrokerElevated()
    {
        LaunchOptions options = LaunchOptions.FromArguments([LaunchOptions.ElevatedBrokerArgument]);

        Assert.True(options.StartHardwareBrokerElevated);
    }

    [Fact]
    public void FromArguments_WithNoDemoteSwitch_SkipsDemotionWithoutDisablingCloseToTray()
    {
        LaunchOptions options = LaunchOptions.FromArguments([UnelevatedRestartService.NoDemoteArgument]);

        Assert.True(options.SkipElevationDemotion);
        Assert.True(options.AllowMultipleInstances);
        Assert.False(options.DisableCloseToTray);
    }
}
