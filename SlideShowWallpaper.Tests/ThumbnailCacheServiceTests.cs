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
        Assert.EndsWith(".jpg", path, StringComparison.OrdinalIgnoreCase);
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
        byte[] header = await File.ReadAllBytesAsync(thumbnailPath);
        Assert.True(header.Length > 2);
        Assert.Equal(0xFF, header[0]);
        Assert.Equal(0xD8, header[1]);
    }

    [Fact]
    public async Task GetOrCreateThumbnailAsync_WithNdfVideo_WritesThumbnailWithoutMaterializedMediaCache()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string sourcePath = Path.Combine(root, "source.ndf");
        await File.WriteAllBytesAsync(sourcePath, [0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70]);
        var info = new FileInfo(sourcePath);
        var metadata = new ImageMetadata(info.FullName, info.Name, info.LastWriteTimeUtc, info.Length, MediaKind.Video);
        ImageMetadata? writerMetadata = null;
        string thumbnailRoot = Path.Combine(root, "thumbnails");
        var service = new ThumbnailCacheService(
            thumbnailRoot,
            (sourceMetadata, thumbnail, _, _) =>
            {
                writerMetadata = sourceMetadata;
                File.WriteAllBytes(thumbnail, [1, 2, 3]);
                return Task.CompletedTask;
            });

        string thumbnailPath = await service.GetOrCreateThumbnailAsync(metadata);

        Assert.Equal(sourcePath, writerMetadata?.Path);
        Assert.True(File.Exists(thumbnailPath));
        Assert.False(Directory.Exists(Path.Combine(thumbnailRoot, "media")));
    }

    [Fact]
    public async Task CreateTemporaryThumbnailAsync_WritesJpegOutsideCacheRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string cacheRoot = Path.Combine(root, "thumbnails");
        string sourcePath = Path.Combine(root, "source.png");
        Directory.CreateDirectory(root);
        await File.WriteAllBytesAsync(sourcePath, Convert.FromBase64String(OnePixelPngBase64));
        var info = new FileInfo(sourcePath);
        var metadata = new ImageMetadata(info.FullName, info.Name, info.LastWriteTimeUtc, info.Length);
        var service = new ThumbnailCacheService(
            cacheRoot,
            (_, thumbnail, _, _) =>
            {
                File.WriteAllBytes(thumbnail, [0xFF, 0xD8, 0xFF, 0xD9]);
                return Task.CompletedTask;
            });

        string thumbnailPath = await service.CreateTemporaryThumbnailAsync(metadata);

        Assert.True(File.Exists(thumbnailPath));
        Assert.EndsWith(".jpg", thumbnailPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(cacheRoot, thumbnailPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCacheSizeBytesAsync_WithCachedFiles_ReturnsTotalSize()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string cacheRoot = Path.Combine(root, "thumbnails");
        Directory.CreateDirectory(Path.Combine(cacheRoot, "aa"));
        await File.WriteAllBytesAsync(Path.Combine(cacheRoot, "aa", "first.jpg"), [1, 2, 3]);
        await File.WriteAllBytesAsync(Path.Combine(cacheRoot, "second.jpg"), [4, 5]);
        var service = new ThumbnailCacheService(cacheRoot);

        long size = await service.GetCacheSizeBytesAsync();

        Assert.Equal(5, size);
    }

    [Fact]
    public async Task ClearCacheAsync_RemovesOnlyThumbnailCacheRootFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string cacheRoot = Path.Combine(root, "thumbnails");
        string mediaRoot = Path.Combine(root, "media");
        string cachedFile = Path.Combine(cacheRoot, "aa", "thumb.jpg");
        string sourceFile = Path.Combine(mediaRoot, "wallpaper.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(cachedFile)!);
        Directory.CreateDirectory(mediaRoot);
        await File.WriteAllBytesAsync(cachedFile, [1, 2, 3]);
        await File.WriteAllBytesAsync(sourceFile, [4, 5, 6, 7]);
        var service = new ThumbnailCacheService(cacheRoot);

        await service.ClearCacheAsync();

        Assert.False(File.Exists(cachedFile));
        Assert.True(File.Exists(sourceFile));
        Assert.Equal(0, await service.GetCacheSizeBytesAsync());
    }

    [Fact]
    public void ShouldDeleteThumbnailMedia_WithNormalVideo_ReturnsFalse()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(path, [1, 2, 3]);
        try
        {
            bool shouldDelete = ThumbnailCacheService.ShouldDeleteThumbnailMedia(path);

            Assert.False(shouldDelete);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ShouldDeleteThumbnailMedia_WithNdfVideo_ReturnsTrue()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ndf");
        File.WriteAllBytes(path, [0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70]);
        try
        {
            bool shouldDelete = ThumbnailCacheService.ShouldDeleteThumbnailMedia(path);

            Assert.True(shouldDelete);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void VideoThumbnailGeneration_FallsBackToMediaCompositionWithoutFfmpeg()
    {
        string source = File.ReadAllText(Path.Combine(FindProjectRoot(), "Services", "ThumbnailCacheService.cs"));

        Assert.Contains("private static readonly SemaphoreSlim VideoThumbnailGate = new(1, 1);", source);
        Assert.Contains("await VideoThumbnailGate.WaitAsync(cancellationToken);", source);
        Assert.Contains("VideoThumbnailGate.Release();", source);
        Assert.Contains("CreateSystemVideoThumbnailAsync(sourceFile, thumbnailPath, maxPixelSize, cancellationToken)", source);
        Assert.Contains("CreateVideoFrameThumbnailAsync(sourceFile, thumbnailPath, maxPixelSize, cancellationToken)", source);
        Assert.Contains("ThumbnailOptions.ReturnOnlyIfCached | ThumbnailOptions.UseCurrentScale", source);
        Assert.Contains("MediaClip.CreateFromFileAsync(sourceFile)", source);
        Assert.Contains("new MediaComposition", source);
        Assert.Contains("VideoFramePrecision.NearestKeyFrame", source);
        Assert.Contains("TimeSpan.FromSeconds(3)", source);
        Assert.DoesNotContain("ffmpeg", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImageThumbnailGeneration_DecodesImageBeforeShellFallback()
    {
        string source = File.ReadAllText(Path.Combine(FindProjectRoot(), "Services", "ThumbnailCacheService.cs"));

        Assert.Contains("CreateImageThumbnailFromDecodedStreamAsync(sourcePath, thumbnailPath, maxPixelSize, cancellationToken)", source);
        Assert.Contains("CreateImageThumbnailFromShellAsync(sourcePath, thumbnailPath, maxPixelSize, cancellationToken)", source);
        Assert.Contains("ThumbnailMode.PicturesView", source);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SlideShowWallpaper.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Project root not found.");
    }
}
