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
    public void WallpaperWindow_AnchorsWallpaperElementsAtTopLeftForCalculatedOffsets()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml"));

        Assert.Equal(3, CountOccurrences(source, "HorizontalAlignment=\"Left\""));
        Assert.Equal(3, CountOccurrences(source, "VerticalAlignment=\"Top\""));
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

    private static int CountOccurrences(string source, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
