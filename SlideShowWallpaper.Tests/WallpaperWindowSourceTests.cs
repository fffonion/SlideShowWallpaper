namespace SlideShowWallpaper.Tests;

public sealed class WallpaperWindowSourceTests
{
    [Fact]
    public void WallpaperWindow_UsesFillStretchForComputedElementSize()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml.cs"));

        Assert.Contains("CurrentImage.Stretch = Stretch.Fill", source);
        Assert.Contains("NextImage.Stretch = Stretch.Fill", source);
        Assert.Contains("VideoPlayer.Stretch = Stretch.Fill", source);
        Assert.DoesNotContain("Stretch = Stretch.None", source);
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
