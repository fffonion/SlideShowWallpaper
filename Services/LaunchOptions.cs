namespace SlideShowWallpaper.Services;

public sealed record LaunchOptions(bool StartInTray)
{
    public static LaunchOptions FromArguments(IEnumerable<string> arguments)
    {
        return new LaunchOptions(arguments.Any(argument => string.Equals(argument, "/q", StringComparison.OrdinalIgnoreCase)));
    }
}
