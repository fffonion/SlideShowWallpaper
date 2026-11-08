using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using Windows.Graphics.Imaging;

namespace SlideShowWallpaper.Tests;

public sealed class ImageOrderServiceTests
{
    [Fact]
    public async Task GetOrLoadOrderedImagesAsync_ConcurrentLegacyCacheLoads_KeepBackgroundScanSignal()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = await ImageTestFiles.CreateAsync(folder, "image.heic", BitmapEncoder.TiffEncoderId);
        string cachePath = Path.Combine(folder, "cache.json");
        var cache = new MediaLibraryCacheService(cachePath);
        cache.Save(folder, false, [new ImageMetadata(path, "image.heic", DateTime.UtcNow, new FileInfo(path).Length)]);
        MediaLibraryCacheServiceTests.RemoveDimensionsVersion(cachePath);
        var service = new ImageOrderService(new ZeroRandom(), cache);

        Task<ImageOrderLoadResult> first = service.GetOrLoadOrderedImagesWithStatusAsync(folder, PlaybackOrder.NameAsc, PlaybackMediaFilter.PortraitImagesOnly, false, CancellationToken.None);
        Task<ImageOrderLoadResult> second = service.GetOrLoadOrderedImagesWithStatusAsync(folder, PlaybackOrder.NameAsc, PlaybackMediaFilter.PortraitImagesOnly, false, CancellationToken.None);
        ImageOrderLoadResult[] results = await Task.WhenAll(first, second);

        Assert.All(results, result =>
        {
            Assert.True(result.LoadedFromCache);
            Assert.Single(result.Images);
        });
    }

    [Fact]
    public async Task GetOrLoadOrderedImagesAsync_LegacyUnknownDimensions_RepairsBeforeOrientationFilter()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = await ImageTestFiles.CreateAsync(folder, "image.heic", BitmapEncoder.TiffEncoderId);
        string cachePath = Path.Combine(folder, "cache.json");
        var cache = new MediaLibraryCacheService(cachePath);
        var file = new FileInfo(path);
        cache.Save(folder, false, [new ImageMetadata(path, file.Name, file.LastWriteTimeUtc, file.Length)]);
        MediaLibraryCacheServiceTests.RemoveDimensionsVersion(cachePath);
        var service = new ImageOrderService(new ZeroRandom(), cache);

        ImageOrderLoadResult result = await service.GetOrLoadOrderedImagesWithStatusAsync(folder, PlaybackOrder.NameAsc, PlaybackMediaFilter.PortraitImagesOnly, false, CancellationToken.None);

        Assert.True(result.LoadedFromCache);
        ImageMetadata image = Assert.Single(result.Images);
        Assert.Equal(40, image.Width);
        Assert.Equal(80, image.Height);
        Assert.True(cache.TryLoad(folder, false, out IReadOnlyList<ImageMetadata> repaired, out bool hasCurrentDimensions));
        Assert.True(hasCurrentDimensions);
        Assert.Equal(40, Assert.Single(repaired).Width);
    }

    [Fact]
    public async Task ReloadOrderedImagesAsync_WithRotatedJpeg_UsesDisplayOrientation()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        await ImageTestFiles.CreateAsync(folder, "rotated.jpg", BitmapEncoder.JpegEncoderId, orientation: 6);
        var service = new ImageOrderService(new ZeroRandom(), new MediaLibraryCacheService(Path.Combine(folder, "cache.json")));

        IReadOnlyList<ImageMetadata> media = await service.ReloadOrderedImagesAsync(folder, PlaybackOrder.NameAsc, PlaybackMediaFilter.LandscapeImagesOnly, CancellationToken.None);

        ImageMetadata image = Assert.Single(media);
        Assert.Equal(80, image.Width);
        Assert.Equal(40, image.Height);
    }

    [Fact]
    public async Task GetOrLoadOrderedImagesAsync_LegacyRotatedDimensions_RepairsBeforeLandscapeFilter()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = await ImageTestFiles.CreateAsync(folder, "rotated.jpg", BitmapEncoder.JpegEncoderId, orientation: 6);
        string cachePath = Path.Combine(folder, "cache.json");
        var cache = new MediaLibraryCacheService(cachePath);
        var file = new FileInfo(path);
        cache.Save(folder, false, [new ImageMetadata(path, file.Name, file.LastWriteTimeUtc, file.Length, MediaKind.Image, 40, 80)]);
        MediaLibraryCacheServiceTests.RemoveDimensionsVersion(cachePath);
        var service = new ImageOrderService(new ZeroRandom(), cache);

        IReadOnlyList<ImageMetadata> media = await service.GetOrLoadOrderedImagesAsync(folder, PlaybackOrder.NameAsc, PlaybackMediaFilter.LandscapeImagesOnly, CancellationToken.None);

        ImageMetadata image = Assert.Single(media);
        Assert.Equal(80, image.Width);
        Assert.Equal(40, image.Height);
    }

    [Fact]
    public async Task ReloadOrderedImagesAsync_UnchangedFile_ReusesCurrentCachedDimensions()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "cached.heic");
        await File.WriteAllBytesAsync(path, [0, 0]);
        var file = new FileInfo(path);
        var cache = new MediaLibraryCacheService(Path.Combine(folder, "cache.json"));
        cache.Save(folder, false, [new ImageMetadata(path, file.Name, file.LastWriteTimeUtc, file.Length, MediaKind.Image, 40, 80)]);
        var service = new ImageOrderService(new ZeroRandom(), cache);

        IReadOnlyList<ImageMetadata> media = await service.ReloadOrderedImagesAsync(folder, PlaybackOrder.NameAsc, PlaybackMediaFilter.PortraitImagesOnly, CancellationToken.None);

        ImageMetadata image = Assert.Single(media);
        Assert.Equal(40, image.Width);
        Assert.Equal(80, image.Height);
    }

    [Fact]
    public async Task GetOrLoadOrderedImagesAsync_AfterReload_ReturnsSameRandomOrderWithoutSecondLoad()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "a.png"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(folder, "b.png"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(folder, "c.png"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(folder, "d.png"), string.Empty);
        var service = new ImageOrderService(new ZeroRandom());

        IReadOnlyList<ImageMetadata> first = await service.ReloadOrderedImagesAsync(folder, PlaybackOrder.Random, CancellationToken.None);
        IReadOnlyList<ImageMetadata> second = await service.GetOrLoadOrderedImagesAsync(folder, PlaybackOrder.Random, CancellationToken.None);

        Assert.Equal(first.Select(image => image.Path), second.Select(image => image.Path));
    }

    [Fact]
    public async Task ClearCache_AfterFolderContentsChange_ForcesNextScan()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string imagePath = Path.Combine(folder, "a.png");
        await File.WriteAllTextAsync(imagePath, string.Empty);
        var service = new ImageOrderService(new ZeroRandom());

        IReadOnlyList<ImageMetadata> first = await service.GetOrLoadOrderedImagesAsync(folder, PlaybackOrder.NameAsc, CancellationToken.None);
        File.Delete(imagePath);
        service.ClearCache();
        IReadOnlyList<ImageMetadata> second = await service.GetOrLoadOrderedImagesAsync(folder, PlaybackOrder.NameAsc, CancellationToken.None);

        Assert.Single(first);
        Assert.Empty(second);
    }

    [Fact]
    public async Task GetOrLoadOrderedImagesAsync_WithImagesOnlyFilter_ReturnsImagesOnly()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "a.png"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(folder, "b.mp4"), string.Empty);
        var service = new ImageOrderService(new ZeroRandom());

        IReadOnlyList<ImageMetadata> media = await service.GetOrLoadOrderedImagesAsync(folder, PlaybackOrder.NameAsc, PlaybackMediaFilter.ImagesOnly, CancellationToken.None);

        ImageMetadata item = Assert.Single(media);
        Assert.Equal("a.png", item.FileName);
    }

    [Fact]
    public async Task GetOrLoadOrderedImagesAsync_WithVideosOnlyFilter_ReturnsVideosOnly()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "a.png"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(folder, "b.mp4"), string.Empty);
        var service = new ImageOrderService(new ZeroRandom());

        IReadOnlyList<ImageMetadata> media = await service.GetOrLoadOrderedImagesAsync(folder, PlaybackOrder.NameAsc, PlaybackMediaFilter.VideosOnly, CancellationToken.None);

        ImageMetadata item = Assert.Single(media);
        Assert.Equal("b.mp4", item.FileName);
    }

    [Fact]
    public async Task GetOrLoadOrderedImagesAsync_MediaFilterUsesSeparateCacheEntry()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "a.png"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(folder, "b.mp4"), string.Empty);
        var service = new ImageOrderService(new ZeroRandom());

        IReadOnlyList<ImageMetadata> all = await service.GetOrLoadOrderedImagesAsync(folder, PlaybackOrder.NameAsc, PlaybackMediaFilter.ImagesAndVideos, CancellationToken.None);
        IReadOnlyList<ImageMetadata> imagesOnly = await service.GetOrLoadOrderedImagesAsync(folder, PlaybackOrder.NameAsc, PlaybackMediaFilter.ImagesOnly, CancellationToken.None);

        Assert.Equal(2, all.Count);
        Assert.Single(imagesOnly);
    }

    [Fact]
    public async Task GetOrLoadOrderedImagesAsync_WithPortraitFilter_UsesCachedDimensions()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string cachePath = Path.Combine(folder, "cache.json");
        Directory.CreateDirectory(folder);
        var cache = new MediaLibraryCacheService(cachePath);
        cache.Save(
            folder,
            includeSubdirectories: false,
            [
                new ImageMetadata(Path.Combine(folder, "portrait.ndf"), "portrait.ndf", DateTime.UtcNow, 1, MediaKind.Image, 1080, 1920),
                new ImageMetadata(Path.Combine(folder, "landscape.ndf"), "landscape.ndf", DateTime.UtcNow, 1, MediaKind.Image, 1920, 1080),
            ]);
        var service = new ImageOrderService(new ZeroRandom(), cache);

        ImageOrderLoadResult result = await service.GetOrLoadOrderedImagesWithStatusAsync(
            folder,
            PlaybackOrder.NameAsc,
            PlaybackMediaFilter.PortraitImagesOnly,
            includeSubdirectories: false,
            CancellationToken.None);

        Assert.True(result.LoadedFromCache);
        ImageMetadata item = Assert.Single(result.Images);
        Assert.Equal("portrait.ndf", item.FileName);
    }

    [Fact]
    public async Task GetOrLoadOrderedImagesAsync_WithRecursiveEnabled_ReturnsNestedFiles()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string child = Path.Combine(folder, "child");
        Directory.CreateDirectory(child);
        await File.WriteAllTextAsync(Path.Combine(folder, "root.png"), string.Empty);
        await File.WriteAllTextAsync(Path.Combine(child, "nested.png"), string.Empty);
        var service = new ImageOrderService(new ZeroRandom());

        IReadOnlyList<ImageMetadata> media = await service.GetOrLoadOrderedImagesAsync(
            folder,
            PlaybackOrder.NameAsc,
            PlaybackMediaFilter.ImagesOnly,
            includeSubdirectories: true,
            CancellationToken.None);

        Assert.Equal(["nested.png", "root.png"], media.Select(item => item.FileName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReloadOrderedImagesAsync_WithUnavailableFolder_UsesMediaCache()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string folder = Path.Combine(root, "network-share");
        string cachePath = Path.Combine(root, "cache.json");
        var cache = new MediaLibraryCacheService(cachePath);
        cache.Save(
            folder,
            includeSubdirectories: false,
            [new ImageMetadata(Path.Combine(folder, "cached.png"), "cached.png", DateTime.UtcNow, 1, MediaKind.Image)]);
        var service = new ImageOrderService(new ZeroRandom(), cache);

        IReadOnlyList<ImageMetadata> media = await service.ReloadOrderedImagesAsync(
            folder,
            PlaybackOrder.NameAsc,
            PlaybackMediaFilter.ImagesOnly,
            includeSubdirectories: false,
            CancellationToken.None);

        ImageMetadata item = Assert.Single(media);
        Assert.Equal("cached.png", item.FileName);
    }

    private sealed class ZeroRandom : Random
    {
        public override int Next(int maxValue)
        {
            return 0;
        }
    }
}
