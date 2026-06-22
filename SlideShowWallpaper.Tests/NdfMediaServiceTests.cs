using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class NdfMediaServiceTests
{
    [Fact]
    public void TryGetMediaInfo_WithNdfMp4_DetectsVideoAndOffset()
    {
        using TestFile file = TestFile.Create(".ndf", [0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70]);

        bool detected = NdfMediaService.TryGetMediaInfo(file.Path, out NdfMediaInfo info);

        Assert.True(detected);
        Assert.Equal(MediaKind.Video, info.Kind);
        Assert.Equal(2, info.Offset);
        Assert.Equal(".mp4", info.Extension);
    }

    [Fact]
    public void TryGetMediaInfo_WithNdfPng_DetectsImageWithoutOffset()
    {
        using TestFile file = TestFile.Create(".ndf", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        bool detected = NdfMediaService.TryGetMediaInfo(file.Path, out NdfMediaInfo info);

        Assert.True(detected);
        Assert.Equal(MediaKind.Image, info.Kind);
        Assert.Equal(0, info.Offset);
        Assert.Equal(".png", info.Extension);
    }

    [Fact]
    public async Task MaterializeForPlaybackAsync_WithNdfMp4_StripsN0vaHeaderOnDemand()
    {
        using TestFile file = TestFile.Create(".ndf", [0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70]);
        string cacheRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        string materialized = await NdfMediaService.MaterializeForPlaybackAsync(file.Path, cacheRoot, CancellationToken.None);

        Assert.EndsWith(".mp4", materialized, StringComparison.OrdinalIgnoreCase);
        Assert.Equal([0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70], await File.ReadAllBytesAsync(materialized));
    }

    [Fact]
    public async Task MaterializeForPlaybackAsync_WithExistingCachedFile_ReusesMaterializedFile()
    {
        using TestFile file = TestFile.Create(".ndf", [0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70]);
        string cacheRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string materialized = await NdfMediaService.MaterializeForPlaybackAsync(file.Path, cacheRoot, CancellationToken.None);
        await File.WriteAllBytesAsync(materialized, [9, 8, 7], CancellationToken.None);

        string secondMaterialized = await NdfMediaService.MaterializeForPlaybackAsync(file.Path, cacheRoot, CancellationToken.None);

        Assert.Equal(materialized, secondMaterialized);
        Assert.Equal([9, 8, 7], await File.ReadAllBytesAsync(secondMaterialized));
    }

    [Fact]
    public async Task GetStorageFileForThumbnailAsync_WithNdfMp4_StreamsStrippedContent()
    {
        using TestFile file = TestFile.Create(".ndf", [0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70]);
        string cacheRoot = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));

        global::Windows.Storage.StorageFile streamedFile = await NdfMediaService.GetStorageFileForThumbnailAsync(file.Path, cacheRoot, CancellationToken.None);

        using global::Windows.Storage.Streams.IRandomAccessStream stream = await streamedFile.OpenReadAsync().AsTask();
        using Stream reader = stream.AsStreamForRead();
        using var memory = new MemoryStream();
        await reader.CopyToAsync(memory);
        Assert.Equal([0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70], memory.ToArray());
        Assert.StartsWith(cacheRoot, streamedFile.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStorageFileForThumbnailAsync_WithVideoSymlink_UsesTargetFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string targetPath = Path.Combine(root, "target.mp4");
        string linkPath = Path.Combine(root, "linked.mp4");
        await File.WriteAllBytesAsync(targetPath, [1, 2, 3, 4, 5]);
        File.CreateSymbolicLink(linkPath, targetPath);

        global::Windows.Storage.StorageFile file = await NdfMediaService.GetStorageFileForThumbnailAsync(linkPath, Path.Combine(root, "cache"), CancellationToken.None);
        ulong size = (await file.GetBasicPropertiesAsync().AsTask()).Size;

        Assert.Equal(targetPath, file.Path);
        Assert.Equal(5UL, size);
    }

    [Fact]
    public async Task OpenContentStreamAsync_WithNdfPng_ReturnsReadableOpenStream()
    {
        using TestFile file = TestFile.Create(".ndf", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        using global::Windows.Storage.Streams.IRandomAccessStream stream = await NdfMediaService.OpenContentStreamAsync(file.Path, CancellationToken.None);
        using Stream reader = stream.AsStreamForRead();
        var bytes = new byte[8];
        int bytesRead = await reader.ReadAsync(bytes);

        Assert.Equal(8, bytesRead);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], bytes);
    }

    [Fact]
    public async Task MaterializeForPlaybackAsync_WithNormalMedia_ReturnsOriginalPath()
    {
        using TestFile file = TestFile.Create(".jpg", [0xFF, 0xD8, 0xFF]);

        string materialized = await NdfMediaService.MaterializeForPlaybackAsync(file.Path, CancellationToken.None);

        Assert.Equal(file.Path, materialized);
    }

    [Fact]
    public async Task MaterializeForPlaybackAsync_WithVideoSymlink_ReturnsTargetPath()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string targetPath = Path.Combine(root, "target.mp4");
        string linkPath = Path.Combine(root, "linked.mp4");
        await File.WriteAllBytesAsync(targetPath, [1, 2, 3, 4, 5]);
        File.CreateSymbolicLink(linkPath, targetPath);

        string materialized = await NdfMediaService.MaterializeForPlaybackAsync(linkPath, CancellationToken.None);

        Assert.Equal(targetPath, materialized);
    }

    private sealed class TestFile : IDisposable
    {
        private TestFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TestFile Create(string extension, byte[] bytes)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
            File.WriteAllBytes(path, bytes);
            return new TestFile(path);
        }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
