using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class HardwareOverlayLayoutCalculatorTests
{
    [Fact]
    public void Calculate_WithBackgroundSize_UsesBackgroundSize()
    {
        HardwareOverlayLayout layout = HardwareOverlayLayoutCalculator.Calculate(
            [CreateElement(10, 10, 80, 30)],
            backgroundWidth: 640,
            backgroundHeight: 360);

        Assert.Equal(640, layout.Width);
        Assert.Equal(360, layout.Height);
    }

    [Fact]
    public void Calculate_WithoutBackgroundSize_UsesElementBounds()
    {
        HardwareOverlayLayout layout = HardwareOverlayLayoutCalculator.Calculate(
            [
                CreateElement(20, 30, 100, 40),
                CreateElement(180, 70, 60, 24),
            ]);

        Assert.Equal(256, layout.Width);
        Assert.Equal(110, layout.Height);
    }

    private static HardwareOverlayElementState CreateElement(double x, double y, double width, double height)
    {
        return new HardwareOverlayElementState(
            Guid.NewGuid().ToString("N"),
            HardwareOverlayElementKind.Text,
            "Text",
            string.Empty,
            HardwareOverlayIconKind.Generic,
            x,
            y,
            width,
            height,
            "Segoe UI",
            16,
            "#FFFFFFFF",
            1);
    }
}
