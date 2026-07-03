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
            new() { Id = "display1", DisplayName = "Dell U2723QE", FolderPath = @"C:\Wallpapers", CurrentMediaIndex = 2, TotalMediaCount = 5 },
        ];

        IReadOnlyList<TrayMenuItem> items = TrayIconService.BuildMenuItems(profiles, GetTestString);

        TrayMenuItem header = Assert.Single(items, item => item.Kind == TrayMenuItemKind.Header);
        Assert.Equal("Dell U2723QE", header.Text);
        Assert.False(header.IsEnabled);
        Assert.Contains(items, item => item.Text == "2/5" && !item.IsEnabled);
        Assert.Contains(items, item => item.Text == "Stop" && item.MonitorId == "display1");
        Assert.Contains(items, item => item.Text == "Pause" && item.MonitorId == "display1");
        Assert.Contains(items, item => item.Text == "Next" && item.MonitorId == "display1");
        Assert.Contains(items, item => item.Text == "Stop" && item.Icon == TrayMenuIcon.Stop && item.DisplayText.Length > item.Text.Length);
        Assert.Contains(items, item => item.Text == "Pause" && item.Icon == TrayMenuIcon.Pause && item.DisplayText.Length > item.Text.Length);
        Assert.Contains(items, item => item.Text == "Next" && item.Icon == TrayMenuIcon.Next && item.DisplayText.Length > item.Text.Length);
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

        IReadOnlyList<TrayMenuItem> items = TrayIconService.BuildMenuItems(profiles, GetTestString);

        Assert.Equal(TrayMenuItemKind.Separator, items[5].Kind);
        Assert.Equal(TrayMenuItemKind.Header, items[6].Kind);
        Assert.Equal("LG UltraFine", items[6].Text);
        Assert.Equal(TrayMenuItemKind.Separator, items[^1].Kind);
    }

    [Fact]
    public void BuildMenuItems_WithMissingFolder_ShowsDisabledNotLoadedOnly()
    {
        MonitorProfile[] profiles =
        [
            new() { Id = "display1", DisplayName = "Dell U2723QE", FolderPath = "" },
        ];

        IReadOnlyList<TrayMenuItem> items = TrayIconService.BuildMenuItems(profiles, GetTestString);

        TrayMenuItem notLoaded = Assert.Single(items, item => item.Text == "Not Loaded");
        Assert.Equal(TrayMenuItemKind.Command, notLoaded.Kind);
        Assert.False(notLoaded.IsEnabled);
        Assert.DoesNotContain(items, item => item.Text is "Start" or "Stop" or "Pause" or "Resume" or "Next");
        Assert.Equal(TrayMenuItemKind.Separator, items[^1].Kind);
    }

    [Fact]
    public void BuildGlobalMenuItems_AddsOpenPauseStopAndExitInOrder()
    {
        MonitorProfile[] profiles =
        [
            new() { Id = "display1", DisplayName = "Dell U2723QE", FolderPath = @"C:\Wallpapers" },
            new() { Id = "display2", DisplayName = "LG UltraFine", FolderPath = @"D:\Pictures", IsPaused = true, IsStopped = true },
        ];

        IReadOnlyList<TrayMenuItem> items = TrayIconService.BuildGlobalMenuItems(profiles, GetTestString);

        Assert.Collection(
            items,
            item =>
            {
                Assert.Equal("Open", item.Text);
                Assert.Equal(TrayMenuIcon.Open, item.Icon);
                Assert.True(item.IsEnabled);
            },
            item =>
            {
                Assert.Equal("Pause All", item.Text);
                Assert.Equal(TrayMenuIcon.Pause, item.Icon);
                Assert.True(item.IsEnabled);
            },
            item =>
            {
                Assert.Equal("Stop All", item.Text);
                Assert.Equal(TrayMenuIcon.Stop, item.Icon);
                Assert.True(item.IsEnabled);
            },
            item =>
            {
                Assert.Equal("Exit", item.Text);
                Assert.Equal(TrayMenuIcon.Exit, item.Icon);
                Assert.True(item.IsEnabled);
            });
    }

    [Fact]
    public void BuildGlobalMenuItems_WithAllPausedAndStoppedProfiles_UsesResumeAndStart()
    {
        MonitorProfile[] profiles =
        [
            new() { Id = "display1", DisplayName = "Dell U2723QE", FolderPath = @"C:\Wallpapers", IsPaused = true, IsStopped = true },
            new() { Id = "display2", DisplayName = "LG UltraFine", FolderPath = @"D:\Pictures", IsPaused = true, IsStopped = true },
        ];

        IReadOnlyList<TrayMenuItem> items = TrayIconService.BuildGlobalMenuItems(profiles, GetTestString);

        Assert.Equal("Resume All", items[1].Text);
        Assert.Equal(TrayMenuIcon.Start, items[1].Icon);
        Assert.Equal("Start All", items[2].Text);
        Assert.Equal(TrayMenuIcon.Start, items[2].Icon);
    }

    [Fact]
    public void BuildGlobalMenuItems_WithNoLoadedProfiles_DisablesGlobalPauseAndStop()
    {
        MonitorProfile[] profiles =
        [
            new() { Id = "display1", DisplayName = "Dell U2723QE", FolderPath = "" },
        ];

        IReadOnlyList<TrayMenuItem> items = TrayIconService.BuildGlobalMenuItems(profiles, GetTestString);

        Assert.True(items[0].IsEnabled);
        Assert.False(items[1].IsEnabled);
        Assert.Equal("Pause All", items[1].Text);
        Assert.Equal(TrayMenuIcon.Pause, items[1].Icon);
        Assert.False(items[2].IsEnabled);
        Assert.Equal("Stop All", items[2].Text);
        Assert.Equal(TrayMenuIcon.Stop, items[2].Icon);
        Assert.True(items[3].IsEnabled);
    }

    private static string GetTestString(string key)
    {
        return key switch
        {
            "Start" => "Start",
            "Stop" => "Stop",
            "Pause" => "Pause",
            "Resume" => "Resume",
            "StartAll" => "Start All",
            "StopAll" => "Stop All",
            "PauseAll" => "Pause All",
            "ResumeAll" => "Resume All",
            "Next" => "Next",
            "NotLoaded" => "Not Loaded",
            "CurrentIndexFormat" => "{0}/{1}",
            _ => key,
        };
    }
}
