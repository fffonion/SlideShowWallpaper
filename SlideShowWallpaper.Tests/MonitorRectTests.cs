using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class MonitorRectTests
{
    [Fact]
    public void ToDesktopRect_WithHighDpiMonitor_PreservesPhysicalPixels()
    {
        var physical = new NativeMethods.RECT
        {
            Left = 891,
            Top = 2160,
            Right = 2811,
            Bottom = 2880,
        };

        NativeMethods.RECT desktop = MonitorService.ToDesktopRect(physical);

        Assert.Equal(891, desktop.Left);
        Assert.Equal(2160, desktop.Top);
        Assert.Equal(1920, desktop.Width);
        Assert.Equal(720, desktop.Height);
    }

    [Fact]
    public void ToDesktopRect_WithFractionalScalePreservesFullEdges()
    {
        var physical = new NativeMethods.RECT
        {
            Left = -1920,
            Top = 0,
            Right = 0,
            Bottom = 1080,
        };

        NativeMethods.RECT desktop = MonitorService.ToDesktopRect(physical);

        Assert.Equal(-1920, desktop.Left);
        Assert.Equal(0, desktop.Top);
        Assert.Equal(1920, desktop.Width);
        Assert.Equal(1080, desktop.Height);
    }
}
