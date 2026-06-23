using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class PreviewPopupLayoutCalculatorTests
{
    [Fact]
    public void CalculateSurface_WithLandscapeMedia_UsesLandscapePopup()
    {
        PreviewPopupSurfaceLayout layout = PreviewPopupLayoutCalculator.CalculateSurface(1920, 1080, 420, 260, 300, 420);

        Assert.Equal(420, layout.Width, 2);
        Assert.Equal(260, layout.Height, 2);
    }

    [Fact]
    public void CalculateSurface_WithPortraitMedia_UsesPortraitPopup()
    {
        PreviewPopupSurfaceLayout layout = PreviewPopupLayoutCalculator.CalculateSurface(1080, 1920, 420, 260, 300, 420);

        Assert.Equal(300, layout.Width, 2);
        Assert.Equal(420, layout.Height, 2);
    }

    [Fact]
    public void CalculateSurface_WithUnknownMediaSize_UsesLandscapePopup()
    {
        PreviewPopupSurfaceLayout layout = PreviewPopupLayoutCalculator.CalculateSurface(0, 0, 420, 260, 300, 420);

        Assert.Equal(420, layout.Width, 2);
        Assert.Equal(260, layout.Height, 2);
    }

    [Fact]
    public void Calculate_WithLandscapeMedia_FitsWidthWithoutChangingAspectRatio()
    {
        PreviewPopupMediaLayout layout = PreviewPopupLayoutCalculator.Calculate(1920, 1080, 402, 242);

        Assert.Equal(402, layout.Width, 2);
        Assert.Equal(226.125, layout.Height, 3);
    }

    [Fact]
    public void Calculate_WithPortraitMedia_FitsHeightWithoutChangingAspectRatio()
    {
        PreviewPopupMediaLayout layout = PreviewPopupLayoutCalculator.Calculate(1080, 1920, 402, 242);

        Assert.Equal(136.125, layout.Width, 3);
        Assert.Equal(242, layout.Height, 2);
    }

    [Fact]
    public void Calculate_WithUnknownMediaSize_UsesFullAvailableArea()
    {
        PreviewPopupMediaLayout layout = PreviewPopupLayoutCalculator.Calculate(0, 0, 402, 242);

        Assert.Equal(402, layout.Width, 2);
        Assert.Equal(242, layout.Height, 2);
    }
}
