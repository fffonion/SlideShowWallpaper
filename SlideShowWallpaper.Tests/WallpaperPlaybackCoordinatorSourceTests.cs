using System.Text.RegularExpressions;

namespace SlideShowWallpaper.Tests;

public sealed class WallpaperPlaybackCoordinatorSourceTests
{
    [Fact]
    public void ConfigureTimer_WithSingleLoop_ReturnsBeforeStartingTimer()
    {
        string source = ReadCoordinatorWindowingSource();

        Assert.Matches(
            new Regex(
                "timer\\.Stop\\(\\);\\s*if \\(profile\\.PlaybackOrder == PlaybackOrder\\.SingleLoop\\)\\s*\\{\\s*return;\\s*\\}.*timer\\.Start\\(\\);",
                RegexOptions.Singleline),
            source);
    }

    [Fact]
    public void RestartTimer_WithSingleLoop_DoesNotRestartTimer()
    {
        string source = ReadCoordinatorWindowingSource();

        Assert.Matches(
            new Regex(
                "\\|\\| profile\\.PlaybackOrder == PlaybackOrder\\.SingleLoop\\s*\\|\\| !_timers\\.TryGetValue.*\\{\\s*return;\\s*\\}.*timer\\.Stop\\(\\);\\s*timer\\.Start\\(\\);",
                RegexOptions.Singleline),
            source);
    }

    [Fact]
    public void EnsureWindow_ConfiguresVideoCoverageTimerAfterCreatingWindow()
    {
        string source = ReadCoordinatorWindowingSource();
        string method = ExtractMethod(source, "private void EnsureWindow", "private async Task<bool> TryShowSelectedImageAsync");

        Assert.Contains("_windows[profile.Id] = window;", method);
        Assert.Contains("ConfigureVideoCoverageTimer();", method);
    }

    [Fact]
    public void HideWindow_HidesWallpaperInsteadOfClosingWindow()
    {
        string source = ReadCoordinatorWindowingSource();
        string method = ExtractMethod(source, "private void HideWindow", "private static void CloseWindowSafely");

        Assert.Contains("window.HideWallpaperWindow();", method);
        Assert.DoesNotContain("window.Close();", method);
        Assert.Contains("ConfigureVideoCoverageTimer();", method);
    }

    [Fact]
    public void ApplyVideoCoverageState_OnlyTouchesShowingWallpaperWindows()
    {
        string source = ReadCoordinatorWindowingSource();
        string method = ExtractMethod(source, "private void ApplyVideoCoverageState", "private void RestartTimer");

        Assert.Contains("window.IsShowingWallpaper", method);
    }

    [Fact]
    public void DisplayPowerResume_RehostsActiveWindows()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "public void SetDisplayPowerVideoPause", "public void PauseOrResume");

        Assert.Contains("RehostActiveWindows();", method);
        Assert.Contains("_desktopHostService.HostOnDesktop(window, monitorId, _monitorRects);", method);
    }

    [Fact]
    public void ManualShowImage_DoesNotApplyExistingWindowProfileBeforeMediaSwitch()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "public async Task ShowImageAsync", "public void Shutdown");

        Assert.Contains("EnsureWindow(profile, applyProfile: false);", method);
        Assert.DoesNotContain("EnsureWindow(profile);", method);
    }

    private static string ReadCoordinatorWindowingSource()
    {
        string root = FindProjectRoot();
        return File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.Windowing.cs"));
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.True(end > start);
        return source[start..end];
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SlideShowWallpaper.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Project root not found.");
    }
}
