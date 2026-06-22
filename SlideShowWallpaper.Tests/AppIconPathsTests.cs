using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class AppIconPathsTests
{
    [Fact]
    public void ResolveShellIconPath_WithExistingProcessPath_ReturnsExecutable()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string exePath = Path.Combine(folder, "SlideShowWallpaper.exe");
        Directory.CreateDirectory(folder);
        File.WriteAllText(exePath, string.Empty);

        string result = AppIconPaths.ResolveShellIconPath(exePath, folder);

        Assert.Equal(exePath, result);
    }

    [Fact]
    public void ResolveShellIconPath_WithoutProcessPath_ReturnsAssetIcon()
    {
        string result = AppIconPaths.ResolveShellIconPath(null, @"C:\App");

        Assert.Equal(@"C:\App\Assets\AppIcon.ico", result);
    }
}
