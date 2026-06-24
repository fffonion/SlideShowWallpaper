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
        var scheduledTaskService = new FakeScheduledTaskService();
        var service = new AutostartService(path, () => @"C:\Apps\SlideShowWallpaper.exe", shortcutFileService, scheduledTaskService);

        service.SetEnabled(true);

        Assert.True(service.IsEnabled());
        Assert.False(service.IsRunAsAdministratorEnabled());
        Assert.Equal(path, shortcutFileService.ShortcutPath);
        Assert.Equal(@"C:\Apps\SlideShowWallpaper.exe", shortcutFileService.TargetPath);
        Assert.Equal("/q", shortcutFileService.Arguments);
        Assert.Equal(@"C:\Apps", shortcutFileService.WorkingDirectory);
        Assert.True(scheduledTaskService.DeleteCalled);
    }

    [Fact]
    public void SetEnabled_WithAdministratorStartup_CreatesScheduledTaskWithQuietArgument()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.lnk");
        var shortcutFileService = new FakeShortcutFileService();
        var scheduledTaskService = new FakeScheduledTaskService();
        var service = new AutostartService(path, () => @"C:\Apps\SlideShowWallpaper.exe", shortcutFileService, scheduledTaskService);

        service.SetEnabled(true, runAsAdministrator: true);

        Assert.True(service.IsEnabled());
        Assert.True(service.IsRunAsAdministratorEnabled());
        Assert.Equal("SlideShowWallpaper", scheduledTaskService.TaskName);
        Assert.Equal(@"C:\Apps\SlideShowWallpaper.exe", scheduledTaskService.TargetPath);
        Assert.Equal("/q", scheduledTaskService.Arguments);
        Assert.Equal(@"C:\Apps", scheduledTaskService.WorkingDirectory);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void SetEnabled_WithTrue_RemovesLegacyCommandFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "SlideShowWallpaper.lnk");
        string legacyPath = Path.Combine(folder, "SlideShowWallpaper.cmd");
        File.WriteAllText(legacyPath, "old");
        var service = new AutostartService(path, () => @"C:\Apps\SlideShowWallpaper.exe", new FakeShortcutFileService(), new FakeScheduledTaskService());

        service.SetEnabled(true);

        Assert.False(File.Exists(legacyPath));
    }

    [Fact]
    public void SetEnabled_WithNativeShortcutService_CreatesReadableShortcut()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "SlideShowWallpaper.lnk");
        string processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Process path is unavailable.");
        var service = new AutostartService(path, () => processPath, new WindowsShortcutFileService(), new FakeScheduledTaskService());

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
        var scheduledTaskService = new FakeScheduledTaskService
        {
            TaskName = "SlideShowWallpaper",
            TargetPath = @"C:\Apps\SlideShowWallpaper.exe",
            Arguments = "/q",
        };
        var service = new AutostartService(path, () => @"C:\Apps\SlideShowWallpaper.exe", new FakeShortcutFileService(), scheduledTaskService);
        service.SetEnabled(true);

        service.SetEnabled(false);

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(legacyPath));
        Assert.False(service.IsEnabled());
        Assert.True(scheduledTaskService.DeleteCalled);
    }

    private sealed class FakeShortcutFileService : IShortcutFileService
    {
        public string ShortcutPath { get; private set; } = string.Empty;

        public string TargetPath { get; set; } = string.Empty;

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

    private sealed class FakeScheduledTaskService : IScheduledTaskService
    {
        public string TaskName { get; set; } = string.Empty;

        public string TargetPath { get; set; } = string.Empty;

        public string Arguments { get; set; } = string.Empty;

        public string WorkingDirectory { get; private set; } = string.Empty;

        public bool DeleteCalled { get; private set; }

        public void CreateLogonTask(string taskName, string targetPath, string arguments, string workingDirectory)
        {
            TaskName = taskName;
            TargetPath = targetPath;
            Arguments = arguments;
            WorkingDirectory = workingDirectory;
        }

        public void Delete(string taskName)
        {
            DeleteCalled = true;
            if (string.Equals(TaskName, taskName, StringComparison.OrdinalIgnoreCase))
            {
                TaskName = string.Empty;
                TargetPath = string.Empty;
                Arguments = string.Empty;
                WorkingDirectory = string.Empty;
            }
        }

        public bool Matches(string taskName, string targetPath, string arguments)
        {
            return string.Equals(TaskName, taskName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(TargetPath, targetPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Arguments, arguments, StringComparison.OrdinalIgnoreCase);
        }
    }
}
