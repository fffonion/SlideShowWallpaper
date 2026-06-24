namespace SlideShowWallpaper.Services;

public sealed record LaunchOptions(bool StartInTray, bool AllowMultipleInstances, bool DisableCloseToTray, bool SkipElevationDemotion)
{
    public static LaunchOptions FromArguments(IEnumerable<string> arguments)
    {
        string[] normalizedArguments = arguments.ToArray();
        bool allowMultipleInstances = normalizedArguments.Any(argument => string.Equals(argument, "/multiple", StringComparison.OrdinalIgnoreCase));
        bool restartElevated = normalizedArguments.Any(argument => string.Equals(argument, AdministratorRestartService.RestartArgument, StringComparison.OrdinalIgnoreCase));
        bool noDemote = normalizedArguments.Any(argument => string.Equals(argument, UnelevatedRestartService.NoDemoteArgument, StringComparison.OrdinalIgnoreCase));
        return new LaunchOptions(
            normalizedArguments.Any(argument => string.Equals(argument, "/q", StringComparison.OrdinalIgnoreCase)),
            allowMultipleInstances || restartElevated || noDemote,
            allowMultipleInstances,
            allowMultipleInstances || noDemote);
    }
}
