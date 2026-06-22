using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class WallpaperTransitionPolicy
{
    public static bool ShouldAnimate(WallpaperTransition transition, int durationMs)
    {
        return transition != WallpaperTransition.None && durationMs > 0;
    }
}
