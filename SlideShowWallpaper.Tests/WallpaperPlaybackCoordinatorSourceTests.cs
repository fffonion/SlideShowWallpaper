using System.Text.RegularExpressions;

namespace SlideShowWallpaper.Tests;

public sealed class WallpaperPlaybackCoordinatorSourceTests
{
    [Fact]
    public void ConfigureTimer_WithSingleLoop_ReturnsBeforeStartingTimer()
    {
        string source = ReadCoordinatorSource();

        Assert.Matches(
            new Regex(
                "timer\\.Stop\\(\\);\\s*if \\(profile\\.PlaybackOrder == PlaybackOrder\\.SingleLoop\\)\\s*\\{\\s*return;\\s*\\}.*timer\\.Start\\(\\);",
                RegexOptions.Singleline),
            source);
    }

    [Fact]
    public void RestartTimer_WithSingleLoop_DoesNotRestartTimer()
    {
        string source = ReadCoordinatorSource();

        Assert.Matches(
            new Regex(
                "\\|\\| profile\\.PlaybackOrder == PlaybackOrder\\.SingleLoop\\s*\\|\\| !_timers\\.TryGetValue.*\\{\\s*return;\\s*\\}.*timer\\.Stop\\(\\);\\s*timer\\.Start\\(\\);",
                RegexOptions.Singleline),
            source);
    }

    [Fact]
    public void EnsureWindow_ConfiguresVideoCoverageTimerAfterCreatingWindow()
    {
        string source = ReadCoordinatorSource();
        string method = ExtractMethod(source, "private void EnsureWindow", "private async void StartRebuildQueue");

        Assert.Contains("_windows[profile.Id] = window;", method);
        Assert.Contains("ConfigureVideoCoverageTimer();", method);
    }

    [Fact]
    public void CloseWindow_ConfiguresVideoCoverageTimerAfterRemovingWindow()
    {
        string source = ReadCoordinatorSource();
        string method = ExtractMethod(source, "private void CloseWindow", "private void ConfigureFolderWatcher");

        Assert.Contains("_windows.Remove(monitorId", method);
        Assert.Contains("ConfigureVideoCoverageTimer();", method);
    }

    private static string ReadCoordinatorSource()
    {
        string root = FindProjectRoot();
        return File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
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
