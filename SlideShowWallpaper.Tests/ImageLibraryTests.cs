using SlideShowWallpaper.Services;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Tests;

public sealed class ImageLibraryTests
{
    [Fact]
    public void IsSupportedImagePath_accepts_static_image_formats_including_heic()
    {
        string[] supported =
        [
            "a.jpg",
            "b.jpeg",
            "c.png",
            "d.bmp",
            "e.webp",
            "f.heic",
            "g.heif",
            "H.JPG",
        ];

        Assert.All(supported, path => Assert.True(ImageLibrary.IsSupportedImagePath(path), path));
    }

    [Fact]
    public void IsSupportedImagePath_rejects_gif_video_and_missing_extensions()
    {
        string[] rejected =
        [
            "a.gif",
            "b.mp4",
            "c.webm",
            "d.mov",
            "e.mkv",
            "f",
            "",
        ];

        Assert.All(rejected, path => Assert.False(ImageLibrary.IsSupportedImagePath(path), path));
    }

    [Fact]
    public void IsSupportedVideoPath_accepts_video_formats_but_rejects_gif()
    {
        string[] supported =
        [
            "a.mp4",
            "b.m4v",
            "c.mov",
            "d.wmv",
            "e.avi",
            "f.mkv",
            "g.webm",
        ];

        Assert.All(supported, path => Assert.True(ImageLibrary.IsSupportedVideoPath(path), path));
        Assert.False(ImageLibrary.IsSupportedVideoPath("animated.gif"));
    }

    [Fact]
    public void ScanFolderMetadata_WithImagesAndVideos_ReturnsMixedMediaWithoutGif()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "a.png"), string.Empty);
        File.WriteAllText(Path.Combine(folder, "b.mp4"), string.Empty);
        File.WriteAllText(Path.Combine(folder, "c.gif"), string.Empty);

        IReadOnlyList<ImageMetadata> media = ImageLibrary.ScanFolderMetadata(folder, PlaybackOrder.NameAsc);

        Assert.Equal(["a.png", "b.mp4"], media.Select(item => item.FileName));
        Assert.Equal([MediaKind.Image, MediaKind.Video], media.Select(item => item.Kind));
    }

    [Fact]
    public void FilterSupportedImages_orders_files_by_full_path()
    {
        string[] paths =
        [
            @"C:\Wallpapers\z.png",
            @"C:\Wallpapers\clip.mp4",
            @"C:\Wallpapers\a.heic",
            @"C:\Wallpapers\b.gif",
        ];

        string[] result = ImageLibrary.FilterSupportedImages(paths).ToArray();

        Assert.Equal([@"C:\Wallpapers\a.heic", @"C:\Wallpapers\z.png"], result);
    }

    [Fact]
    public void SortImages_WithNameDescending_OrdersByFileNameDescending()
    {
        ImageMetadata[] items =
        [
            new(@"C:\Wallpapers\b.png", "b.png", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), 20),
            new(@"C:\Wallpapers\a.png", "a.png", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 10),
        ];

        string[] result = ImageLibrary.SortImages(items, PlaybackOrder.NameDesc).Select(item => item.FileName).ToArray();

        Assert.Equal(["b.png", "a.png"], result);
    }

    [Fact]
    public void SortImages_WithModifiedDateAscending_OrdersByModifiedDate()
    {
        ImageMetadata[] items =
        [
            new(@"C:\Wallpapers\new.png", "new.png", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), 20),
            new(@"C:\Wallpapers\old.png", "old.png", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 10),
        ];

        string[] result = ImageLibrary.SortImages(items, PlaybackOrder.ModifiedDateAsc).Select(item => item.FileName).ToArray();

        Assert.Equal(["old.png", "new.png"], result);
    }

    [Fact]
    public void SortImages_WithRandom_UsesRandomOrder()
    {
        ImageMetadata[] items =
        [
            new(@"C:\Wallpapers\a.png", "a.png", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 10),
            new(@"C:\Wallpapers\b.png", "b.png", new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), 20),
            new(@"C:\Wallpapers\c.png", "c.png", new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc), 30),
            new(@"C:\Wallpapers\d.png", "d.png", new DateTime(2026, 1, 4, 0, 0, 0, DateTimeKind.Utc), 40),
        ];

        string[] result = ImageLibrary.SortImages(items, PlaybackOrder.Random, new ZeroRandom()).Select(item => item.FileName).ToArray();

        Assert.Equal(["b.png", "c.png", "d.png", "a.png"], result);
    }

    private sealed class ZeroRandom : Random
    {
        public override int Next(int maxValue)
        {
            return 0;
        }
    }
}
