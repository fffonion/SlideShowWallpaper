using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class WallpaperLayoutCalculatorTests
{
    [Fact]
    public void Calculate_WithCoverAndLargeHorizontalOffset_ClampsInsideCropOverflow()
    {
        WallpaperElementLayout layout = WallpaperLayoutCalculator.Calculate(
            3840,
            2160,
            1920,
            1200,
            WallpaperScaleMode.Cover,
            500,
            0);

        Assert.Equal(2133.33, layout.Width, 2);
        Assert.Equal(1200, layout.Height, 2);
        Assert.Equal(106.67, layout.OffsetX, 2);
        Assert.Equal(0, layout.OffsetY, 2);
    }

    [Fact]
    public void Calculate_WithCoverAndLargeVerticalOffset_ClampsInsideCropOverflow()
    {
        WallpaperElementLayout layout = WallpaperLayoutCalculator.Calculate(
            1080,
            1920,
            1920,
            1080,
            WallpaperScaleMode.Cover,
            0,
            -2000);

        Assert.Equal(1920, layout.Width, 2);
        Assert.Equal(3413.33, layout.Height, 2);
        Assert.Equal(0, layout.OffsetX, 2);
        Assert.Equal(-1166.67, layout.OffsetY, 2);
    }

    [Fact]
    public void Calculate_WithStretch_IgnoresOffsetToAvoidExposingBackground()
    {
        WallpaperElementLayout layout = WallpaperLayoutCalculator.Calculate(
            800,
            600,
            1920,
            1080,
            WallpaperScaleMode.Stretch,
            100,
            100);

        Assert.Equal(1920, layout.Width, 2);
        Assert.Equal(1080, layout.Height, 2);
        Assert.Equal(0, layout.OffsetX, 2);
        Assert.Equal(0, layout.OffsetY, 2);
    }
}
