using System.Text.Json.Nodes;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class MediaLibraryCacheServiceTests
{
    [Fact]
    public void TryLoad_NewCache_ReportsCurrentDimensions()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        var service = new MediaLibraryCacheService(Path.Combine(root, "cache.json"));
        service.Save(root, false, [new ImageMetadata(Path.Combine(root, "image.heic"), "image.heic", DateTime.UtcNow, 1, MediaKind.Image, 40, 80)]);

        Assert.True(service.TryLoad(root, false, out _, out bool hasCurrentDimensions));
        Assert.True(hasCurrentDimensions);
    }

    [Fact]
    public void TryLoad_LegacyCache_KeepsFileListAndMarksDimensionsForRepair()
    {
        string root = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string cachePath = Path.Combine(root, "cache.json");
        var service = new MediaLibraryCacheService(cachePath);
        service.Save(root, false, [new ImageMetadata(Path.Combine(root, "image.heic"), "image.heic", DateTime.UtcNow, 1)]);
        RemoveDimensionsVersion(cachePath);

        Assert.True(service.TryLoad(root, false, out IReadOnlyList<ImageMetadata> media, out bool hasCurrentDimensions));
        Assert.Single(media);
        Assert.False(hasCurrentDimensions);
    }

    internal static void RemoveDimensionsVersion(string cachePath)
    {
        JsonNode json = JsonNode.Parse(File.ReadAllText(cachePath))!;
        foreach (KeyValuePair<string, JsonNode?> entry in json["entries"]!.AsObject())
        {
            entry.Value!.AsObject().Remove("dimensionsVersion");
        }

        File.WriteAllText(cachePath, json.ToJsonString());
    }

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
