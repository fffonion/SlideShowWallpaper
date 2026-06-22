using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class AutostartServiceTests
{
    [Fact]
    public void SetEnabled_WithTrue_WritesStartupCommandFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.cmd");
        var service = new AutostartService(path, () => @"C:\Apps\SlideShowWallpaper.exe");

        service.SetEnabled(true);

        string content = File.ReadAllText(path);
        Assert.True(service.IsEnabled());
        Assert.Contains(@"C:\Apps\SlideShowWallpaper.exe", content);
        Assert.Contains("/q", content);
    }

    [Fact]
    public void SetEnabled_WithFalse_RemovesStartupCommandFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.cmd");
        var service = new AutostartService(path, () => @"C:\Apps\SlideShowWallpaper.exe");
        service.SetEnabled(true);

        service.SetEnabled(false);

        Assert.False(File.Exists(path));
        Assert.False(service.IsEnabled());
    }
}
