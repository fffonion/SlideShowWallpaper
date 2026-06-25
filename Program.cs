using SlideShowWallpaper.Services;

namespace SlideShowWallpaper;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        LaunchOptions launchOptions = LaunchOptions.FromArguments(args);
        if (launchOptions.SkipElevationDemotion && CurrentProcessPrivilege.IsElevated())
        {
            AppLog.Write("Process is still elevated after elevation demotion was marked complete.");
            return 1;
        }

        if (!launchOptions.SkipElevationDemotion)
        {
            switch (new UnelevatedRestartService().RestartIfCurrentProcessIsElevated(args))
            {
                case UnelevatedRestartResult.Restarted:
                    return 0;
                case UnelevatedRestartResult.Failed:
                    return 1;
            }
        }

        WinUiAppHost.Start();
        return 0;
    }
}
