namespace SlideShowWallpaper.Services;

public static class AppIconPaths
{
    public static string ResolveShellIconPath(string? processPath, string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            return processPath;
        }

        return Path.Combine(baseDirectory, "Assets", "AppIcon.ico");
    }
}
