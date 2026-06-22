using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class TrayMenuTests
{
    [Fact]
    public void BuildMenuItems_WithProfiles_UsesDisabledHeadersAndShortActions()
    {
        MonitorProfile[] profiles =
        [
            new() { Id = "display1", DisplayName = "Dell U2723QE", FolderPath = @"C:\Wallpapers" },
        ];

        IReadOnlyList<TrayMenuItem> items = TrayIconService.BuildMenuItems(profiles);

        TrayMenuItem header = Assert.Single(items, item => item.Kind == TrayMenuItemKind.Header);
        Assert.Equal("Dell U2723QE", header.Text);
        Assert.False(header.IsEnabled);
        Assert.Contains(items, item => item.Text == "Stop" && item.MonitorId == "display1");
        Assert.Contains(items, item => item.Text == "Pause" && item.MonitorId == "display1");
        Assert.Contains(items, item => item.Text == "Next" && item.MonitorId == "display1");
        Assert.DoesNotContain(items, item => item.Kind == TrayMenuItemKind.Command && item.Text.Contains("Dell", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(TrayMenuItemKind.Separator, items[^1].Kind);
    }

    [Fact]
    public void BuildMenuItems_WithMultipleProfiles_AddsSeparatorAfterEachMonitorGroup()
    {
        MonitorProfile[] profiles =
        [
            new() { Id = "display1", DisplayName = "Dell U2723QE", FolderPath = @"C:\Wallpapers" },
            new() { Id = "display2", DisplayName = "LG UltraFine", FolderPath = @"D:\Pictures" },
        ];

        IReadOnlyList<TrayMenuItem> items = TrayIconService.BuildMenuItems(profiles);

        Assert.Equal(TrayMenuItemKind.Separator, items[4].Kind);
        Assert.Equal(TrayMenuItemKind.Header, items[5].Kind);
        Assert.Equal("LG UltraFine", items[5].Text);
        Assert.Equal(TrayMenuItemKind.Separator, items[^1].Kind);
    }

    [Fact]
    public void BuildMenuItems_WithMissingFolder_ShowsDisabledNotLoadedOnly()
    {
        MonitorProfile[] profiles =
        [
            new() { Id = "display1", DisplayName = "Dell U2723QE", FolderPath = "" },
        ];

        IReadOnlyList<TrayMenuItem> items = TrayIconService.BuildMenuItems(profiles);

        TrayMenuItem notLoaded = Assert.Single(items, item => item.Text == "Not Loaded");
        Assert.Equal(TrayMenuItemKind.Command, notLoaded.Kind);
        Assert.False(notLoaded.IsEnabled);
        Assert.DoesNotContain(items, item => item.Text is "Start" or "Stop" or "Pause" or "Resume" or "Next");
        Assert.Equal(TrayMenuItemKind.Separator, items[^1].Kind);
    }
}
