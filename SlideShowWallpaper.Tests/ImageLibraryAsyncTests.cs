using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class ImageLibraryAsyncTests
{
    [Fact]
    public async Task ScanFolderMetadataAsync_WithImages_ReturnsMetadata()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string imagePath = Path.Combine(folder, "wallpaper.png");
        await File.WriteAllTextAsync(imagePath, "not a real image");

        IReadOnlyList<ImageMetadata> images = await ImageLibrary.ScanFolderMetadataAsync(folder, PlaybackOrder.NameAsc, CancellationToken.None);

        ImageMetadata image = Assert.Single(images);
        Assert.Equal(imagePath, image.Path);
    }
}
