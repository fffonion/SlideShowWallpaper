using Microsoft.UI.Xaml.Media;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class WallpaperScaleModeMapperTests
{
    [Fact]
    public void ToImageStretch_WithCover_ReturnsUniformToFill()
    {
        Stretch stretch = WallpaperScaleModeMapper.ToImageStretch(WallpaperScaleMode.Cover);

        Assert.Equal(Stretch.UniformToFill, stretch);
    }
}
