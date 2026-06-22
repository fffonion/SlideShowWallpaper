namespace SlideShowWallpaper.Services;

public sealed record LaunchOptions(bool StartInTray, bool AllowMultipleInstances)
{
    public static LaunchOptions FromArguments(IEnumerable<string> arguments)
    {
        string[] normalizedArguments = arguments.ToArray();
        return new LaunchOptions(
            normalizedArguments.Any(argument => string.Equals(argument, "/q", StringComparison.OrdinalIgnoreCase)),
            normalizedArguments.Any(argument => string.Equals(argument, "/multiple", StringComparison.OrdinalIgnoreCase)));
    }
}
