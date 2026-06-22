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

    [Fact]
    public void WallpaperWindow_AnchorsWallpaperElementsForCanvasOffsets()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml"));

        AssertWallpaperElementAnchored(source, "CurrentImage");
        AssertWallpaperElementAnchored(source, "NextImage");
        AssertWallpaperElementAnchored(source, "VideoPlayer");
    }

    [Fact]
    public void WallpaperWindow_UsesXamlRootViewportForVideoLayout()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml.cs"));
        string applyVideoLayout = source[
            source.IndexOf("private void ApplyVideoLayout", StringComparison.Ordinal)..
            source.IndexOf("private DoubleAnimation CreateOpacityAnimation", StringComparison.Ordinal)];

        Assert.Contains("GetViewportWidth(Root)", applyVideoLayout);
        Assert.Contains("GetViewportHeight(Root)", applyVideoLayout);
        Assert.DoesNotContain("ActualWidthOrFallback()", applyVideoLayout);
        Assert.DoesNotContain("ActualHeightOrFallback()", applyVideoLayout);
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

    private static void AssertWallpaperElementAnchored(string source, string name)
    {
        int nameIndex = source.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
        Assert.True(nameIndex >= 0, $"Could not find {name}.");
        int endIndex = source.IndexOf("/>", nameIndex, StringComparison.Ordinal);
        Assert.True(endIndex > nameIndex, $"Could not find the end of {name}.");
        string element = source[nameIndex..endIndex];

        Assert.Contains("HorizontalAlignment=\"Left\"", element);
        Assert.Contains("VerticalAlignment=\"Top\"", element);
    }
}
