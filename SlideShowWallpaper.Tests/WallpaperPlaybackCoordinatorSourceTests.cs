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

    private static string ReadCoordinatorSource()
    {
        string root = FindProjectRoot();
        return File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
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
