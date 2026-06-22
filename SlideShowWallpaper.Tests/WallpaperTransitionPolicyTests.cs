using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class WallpaperTransitionPolicyTests
{
    [Fact]
    public void ShouldAnimate_WithFadeTransition_ReturnsTrue()
    {
        bool shouldAnimate = WallpaperTransitionPolicy.ShouldAnimate(WallpaperTransition.Fade, 800);

        Assert.True(shouldAnimate);
    }

    [Fact]
    public void ShouldAnimate_WithNoTransition_ReturnsFalse()
    {
        bool shouldAnimate = WallpaperTransitionPolicy.ShouldAnimate(WallpaperTransition.None, 800);

        Assert.False(shouldAnimate);
    }

    [Fact]
    public void ShouldAnimate_WithZeroDuration_ReturnsFalse()
    {
        bool shouldAnimate = WallpaperTransitionPolicy.ShouldAnimate(WallpaperTransition.Slide, 0);

        Assert.False(shouldAnimate);
    }
}
