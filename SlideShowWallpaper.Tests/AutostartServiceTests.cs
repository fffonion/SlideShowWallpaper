using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class AutostartServiceTests
{
    [Fact]
    public void SetEnabled_WithTrue_CreatesStartupShortcutWithQuietArgument()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.lnk");
        var shortcutFileService = new FakeShortcutFileService();
        var service = new AutostartService(path, () => @"C:\Apps\SlideShowWallpaper.exe", shortcutFileService);

        service.SetEnabled(true);

        Assert.True(service.IsEnabled());
        Assert.Equal(path, shortcutFileService.ShortcutPath);
        Assert.Equal(@"C:\Apps\SlideShowWallpaper.exe", shortcutFileService.TargetPath);
        Assert.Equal("/q", shortcutFileService.Arguments);
        Assert.Equal(@"C:\Apps", shortcutFileService.WorkingDirectory);
    }

    [Fact]
    public void SetEnabled_WithTrue_RemovesLegacyCommandFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "SlideShowWallpaper.lnk");
        string legacyPath = Path.Combine(folder, "SlideShowWallpaper.cmd");
        File.WriteAllText(legacyPath, "old");
        var service = new AutostartService(path, () => @"C:\Apps\SlideShowWallpaper.exe", new FakeShortcutFileService());

        service.SetEnabled(true);

        Assert.False(File.Exists(legacyPath));
    }

    [Fact]
    public void SetEnabled_WithNativeShortcutService_CreatesReadableShortcut()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.lnk");
        string processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Process path is unavailable.");
        var service = new AutostartService(path, () => processPath);

        try
        {
            service.SetEnabled(true);

            Assert.True(File.Exists(path));
            Assert.True(service.IsEnabled());
        }
        finally
        {
            service.SetEnabled(false);
        }
    }

    [Fact]
    public void SetEnabled_WithFalse_RemovesStartupShortcutAndLegacyCommandFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "SlideShowWallpaper.lnk");
        string legacyPath = Path.Combine(folder, "SlideShowWallpaper.cmd");
        File.WriteAllText(path, "shortcut");
        File.WriteAllText(legacyPath, "old");
        var service = new AutostartService(path, () => @"C:\Apps\SlideShowWallpaper.exe", new FakeShortcutFileService());
        service.SetEnabled(true);

        service.SetEnabled(false);

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(legacyPath));
        Assert.False(service.IsEnabled());
    }

    private sealed class FakeShortcutFileService : IShortcutFileService
    {
        public string ShortcutPath { get; private set; } = string.Empty;

        public string TargetPath { get; private set; } = string.Empty;

        public string Arguments { get; private set; } = string.Empty;

        public string WorkingDirectory { get; private set; } = string.Empty;

        public void Create(string shortcutPath, string targetPath, string arguments, string workingDirectory)
        {
            ShortcutPath = shortcutPath;
            TargetPath = targetPath;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
            File.WriteAllText(shortcutPath, "shortcut");
        }

        public bool Matches(string shortcutPath, string targetPath, string arguments)
        {
            return File.Exists(shortcutPath)
                && string.Equals(ShortcutPath, shortcutPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(TargetPath, targetPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Arguments, arguments, StringComparison.OrdinalIgnoreCase);
        }
    }
}
