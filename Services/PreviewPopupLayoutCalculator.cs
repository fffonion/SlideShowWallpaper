namespace SlideShowWallpaper.Services;

public static class PreviewPopupLayoutCalculator
{
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

public readonly record struct PreviewPopupMediaLayout(double Width, double Height);
