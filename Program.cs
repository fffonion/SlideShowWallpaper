using SlideShowWallpaper.Services;

namespace SlideShowWallpaper;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        LaunchOptions launchOptions = LaunchOptions.FromArguments(args);
        if (!launchOptions.SkipElevationDemotion
            && new UnelevatedRestartService().TryRestartIfCurrentProcessIsElevated(args))
        {
            return 0;
        }

        WinUiAppHost.Start();
        return 0;
    }
}
