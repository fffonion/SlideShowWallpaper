using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class WallpaperLayoutCalculator
{
    public static WallpaperElementLayout Calculate(
        double sourceWidth,
        double sourceHeight,
        double viewportWidth,
        double viewportHeight,
        WallpaperScaleMode scaleMode,
        double offsetX,
        double offsetY)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return new WallpaperElementLayout(Math.Max(1, viewportWidth), Math.Max(1, viewportHeight), 0, 0);
        }

        double scale = scaleMode switch
        {
            WallpaperScaleMode.Cover => Math.Max(viewportWidth / sourceWidth, viewportHeight / sourceHeight),
            WallpaperScaleMode.Stretch => double.NaN,
            WallpaperScaleMode.Original => 1,
            _ => Math.Min(viewportWidth / sourceWidth, viewportHeight / sourceHeight),
        };

        double width = scaleMode == WallpaperScaleMode.Stretch ? viewportWidth : sourceWidth * scale;
        double height = scaleMode == WallpaperScaleMode.Stretch ? viewportHeight : sourceHeight * scale;
        double clampedOffsetX = ClampOffset(offsetX, width, viewportWidth);
        double clampedOffsetY = ClampOffset(offsetY, height, viewportHeight);
        return new WallpaperElementLayout(width, height, -clampedOffsetX, -clampedOffsetY);
    }

    private static double ClampOffset(double offset, double contentSize, double viewportSize)
    {
        double overflow = Math.Max(0, contentSize - viewportSize);
        double limit = overflow / 2;
        return Math.Clamp(offset, -limit, limit);
    }
}

public readonly record struct WallpaperElementLayout(double Width, double Height, double OffsetX, double OffsetY);
