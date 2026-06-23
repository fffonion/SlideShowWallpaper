namespace SlideShowWallpaper.Services;

public static class PreviewPopupLayoutCalculator
{
    public static PreviewPopupSurfaceLayout CalculateSurface(
        double sourceWidth,
        double sourceHeight,
        double landscapeWidth,
        double landscapeHeight,
        double portraitWidth,
        double portraitHeight)
    {
        if (sourceWidth <= 0
            || sourceHeight <= 0
            || landscapeWidth <= 0
            || landscapeHeight <= 0
            || portraitWidth <= 0
            || portraitHeight <= 0)
        {
            return new PreviewPopupSurfaceLayout(Math.Max(1, landscapeWidth), Math.Max(1, landscapeHeight));
        }

        return sourceHeight > sourceWidth
            ? new PreviewPopupSurfaceLayout(Math.Max(1, portraitWidth), Math.Max(1, portraitHeight))
            : new PreviewPopupSurfaceLayout(Math.Max(1, landscapeWidth), Math.Max(1, landscapeHeight));
    }

    public static PreviewPopupMediaLayout Calculate(
        double sourceWidth,
        double sourceHeight,
        double maxWidth,
        double maxHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || maxWidth <= 0 || maxHeight <= 0)
        {
            return new PreviewPopupMediaLayout(Math.Max(1, maxWidth), Math.Max(1, maxHeight));
        }

        double scale = Math.Min(maxWidth / sourceWidth, maxHeight / sourceHeight);
        return new PreviewPopupMediaLayout(
            Math.Max(1, sourceWidth * scale),
            Math.Max(1, sourceHeight * scale));
    }
}

public readonly record struct PreviewPopupSurfaceLayout(double Width, double Height);

public readonly record struct PreviewPopupMediaLayout(double Width, double Height);
