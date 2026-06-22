using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class ImageOrderServiceTests
{
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

    private sealed class ZeroRandom : Random
    {
        public override int Next(int maxValue)
        {
            return 0;
        }
    }
}
