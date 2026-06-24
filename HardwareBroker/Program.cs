using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.HardwareBroker;

public static class Program
{
    public static int Main(string[] args)
    {
        return HardwareMonitorBrokerHost.Run(args);
    }
}
