namespace SlideShowWallpaper.Services;

public sealed class AutostartService
{
    private const string StartupFileName = "SlideShowWallpaper.cmd";
    private readonly Func<string> _processPathProvider;
    private readonly string _startupFilePath;

    public AutostartService()
        : this(GetDefaultStartupFilePath(), () => Environment.ProcessPath ?? string.Empty)
    {
    }

    public AutostartService(string startupFilePath, Func<string> processPathProvider)
    {
        _startupFilePath = startupFilePath;
        _processPathProvider = processPathProvider;
    }

    public bool IsEnabled()
    {
        string processPath = _processPathProvider();
        return !string.IsNullOrWhiteSpace(processPath)
            && File.Exists(_startupFilePath)
            && File.ReadAllText(_startupFilePath).Contains(processPath, StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            File.Delete(_startupFilePath);
            return;
        }

        string processPath = _processPathProvider();
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return;
        }

        string? folder = Path.GetDirectoryName(_startupFilePath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllText(_startupFilePath, $"@echo off{Environment.NewLine}start \"\" \"{processPath}\" /q{Environment.NewLine}");
    }

    private static string GetDefaultStartupFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", "Startup", StartupFileName);
    }
}
