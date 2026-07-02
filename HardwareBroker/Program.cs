using System.Runtime.Versioning;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.HardwareBroker;

[SupportedOSPlatform("windows")]
public static class Program
{
    public static int Main(string[] args)
    {
        Console.Title = "SlideShowWallpaper Broker";
        return HardwareMonitorBrokerHost.Run(args);
    }
}
