using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class MediaLibraryCacheServiceTests
{
    [Fact]
    public void SaveAndTryLoad_WithFolderAndRecursiveFlag_RoundTripsMetadata()
    {
        string cachePath = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"), "media-cache.json");
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        var service = new MediaLibraryCacheService(cachePath);
        ImageMetadata[] media =
        [
            new(Path.Combine(folder, "portrait.ndf"), "portrait.ndf", new DateTime(2026, 6, 25, 1, 2, 3, DateTimeKind.Utc), 123, MediaKind.Image, 1080, 1920),
        ];

        service.Save(folder, includeSubdirectories: true, media);
        bool loaded = service.TryLoad(folder, includeSubdirectories: true, out IReadOnlyList<ImageMetadata> cached);

        Assert.True(loaded);
        ImageMetadata item = Assert.Single(cached);
        Assert.Equal("portrait.ndf", item.FileName);
        Assert.Equal(1080, item.Width);
        Assert.Equal(1920, item.Height);
    }

    [Fact]
    public void TryLoad_WithDifferentRecursiveFlag_DoesNotReuseCache()
    {
        string cachePath = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"), "media-cache.json");
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        var service = new MediaLibraryCacheService(cachePath);

        service.Save(folder, includeSubdirectories: false, [new ImageMetadata(Path.Combine(folder, "a.png"), "a.png", DateTime.UtcNow, 1)]);
        bool loaded = service.TryLoad(folder, includeSubdirectories: true, out IReadOnlyList<ImageMetadata> cached);

        Assert.False(loaded);
        Assert.Empty(cached);
    }
}
