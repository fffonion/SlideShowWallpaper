using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class HardwareOverlayLayoutCalculator
{
    public const double ElementPadding = 16;

    public static HardwareOverlayLayout Calculate(
        IReadOnlyList<HardwareOverlayElementState> elements,
        int backgroundWidth = 0,
        int backgroundHeight = 0,
        double requestedWidth = 0,
        double requestedHeight = 0)
    {
        if (requestedWidth > 0 && requestedHeight > 0)
        {
            return new HardwareOverlayLayout(requestedWidth, requestedHeight);
        }

        if (backgroundWidth > 0 && backgroundHeight > 0)
        {
            return new HardwareOverlayLayout(backgroundWidth, backgroundHeight);
        }

        if (elements.Count == 0)
        {
            return new HardwareOverlayLayout(1, 1);
        }

        double width = elements.Max(element => Math.Max(0, element.X) + Math.Max(1, element.Width)) + ElementPadding;
        double height = elements.Max(element => Math.Max(0, element.Y) + Math.Max(1, element.Height)) + ElementPadding;
        return new HardwareOverlayLayout(Math.Max(1, width), Math.Max(1, height));
    }
}

public readonly record struct HardwareOverlayLayout(double Width, double Height);
