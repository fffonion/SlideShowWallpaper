using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.HardwareBroker;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.Title = "SlideShowWallpaper Broker";
        return HardwareMonitorBrokerHost.Run(args);
    }
}
