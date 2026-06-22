using Microsoft.UI.Xaml.Media;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class WallpaperScaleModeMapper
{
    public static Stretch ToImageStretch(WallpaperScaleMode scaleMode)
    {
        return scaleMode switch
        {
            WallpaperScaleMode.Cover => Stretch.UniformToFill,
            WallpaperScaleMode.Stretch => Stretch.Fill,
            WallpaperScaleMode.Original => Stretch.None,
            _ => Stretch.Uniform,
        };
    }
}
