using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class ThumbnailCacheServiceTests
{
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    [Fact]
    public void GetThumbnailPath_UsesTempThumbnailFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        var service = new ThumbnailCacheService(root);
        var metadata = new ImageMetadata(@"C:\Pictures\wallpaper.jpg", "wallpaper.jpg", new DateTime(2026, 6, 22, 1, 2, 3, DateTimeKind.Utc), 1234);

        string path = service.GetThumbnailPath(metadata);

        Assert.StartsWith(root, path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".png", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetThumbnailPath_ChangesWhenSourceMetadataChanges()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        var service = new ThumbnailCacheService(root);
        var first = new ImageMetadata(@"C:\Pictures\wallpaper.jpg", "wallpaper.jpg", new DateTime(2026, 6, 22, 1, 2, 3, DateTimeKind.Utc), 1234);
        var second = first with { ModifiedUtc = first.ModifiedUtc.AddSeconds(1) };

        string firstPath = service.GetThumbnailPath(first);
        string secondPath = service.GetThumbnailPath(second);

        Assert.NotEqual(firstPath, secondPath);
    }

    [Fact]
    public async Task GetOrCreateThumbnailAsync_WritesThumbnailFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string sourcePath = Path.Combine(root, "source.png");
        await File.WriteAllBytesAsync(sourcePath, Convert.FromBase64String(OnePixelPngBase64));
        var info = new FileInfo(sourcePath);
        var metadata = new ImageMetadata(info.FullName, info.Name, info.LastWriteTimeUtc, info.Length);
        var service = new ThumbnailCacheService(Path.Combine(root, "thumbnails"));

        string thumbnailPath = await service.GetOrCreateThumbnailAsync(metadata);

        Assert.True(File.Exists(thumbnailPath));
        Assert.StartsWith(Path.Combine(root, "thumbnails"), thumbnailPath, StringComparison.OrdinalIgnoreCase);
    }
}
