namespace SlideShowWallpaper.Services;

public sealed record LaunchOptions(bool StartInTray, bool AllowMultipleInstances, bool DisableCloseToTray)
{
    public static LaunchOptions FromArguments(IEnumerable<string> arguments)
    {
        string[] normalizedArguments = arguments.ToArray();
        bool allowMultipleInstances = normalizedArguments.Any(argument => string.Equals(argument, "/multiple", StringComparison.OrdinalIgnoreCase));
        return new LaunchOptions(
            normalizedArguments.Any(argument => string.Equals(argument, "/q", StringComparison.OrdinalIgnoreCase)),
            allowMultipleInstances,
            allowMultipleInstances);
    }
}
