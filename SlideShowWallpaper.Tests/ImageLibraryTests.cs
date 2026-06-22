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
    public void ScanFolderMetadata_WithVideoSymlink_KeepsLinkNameAndUsesTargetMetadata()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string targetPath = Path.Combine(folder, "target.mp4");
        string linkPath = Path.Combine(folder, "linked.mp4");
        File.WriteAllBytes(targetPath, [1, 2, 3, 4, 5]);
        File.SetLastWriteTimeUtc(targetPath, new DateTime(2026, 6, 23, 1, 2, 3, DateTimeKind.Utc));
        File.CreateSymbolicLink(linkPath, targetPath);

        IReadOnlyList<ImageMetadata> media = ImageLibrary.ScanFolderMetadata(folder, PlaybackOrder.NameAsc);

        ImageMetadata link = Assert.Single(media, item => item.FileName == "linked.mp4");
        Assert.Equal(linkPath, link.Path);
        Assert.Equal(5, link.Length);
        Assert.Equal(File.GetLastWriteTimeUtc(targetPath), link.ModifiedUtc);
        Assert.Equal(MediaKind.Video, link.Kind);
    }

    [Fact]
    public void ScanFolderMetadata_WithNdfMedia_ReturnsDetectedMediaKinds()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "image.ndf"), CreatePngNdf(1920, 1080));
        File.WriteAllBytes(Path.Combine(folder, "video.ndf"), CreateMp4Ndf(1920, 1080));

        IReadOnlyList<ImageMetadata> media = ImageLibrary.ScanFolderMetadata(folder, PlaybackOrder.NameAsc);

        Assert.Equal(["image.ndf", "video.ndf"], media.Select(item => item.FileName));
        Assert.Equal([MediaKind.Image, MediaKind.Video], media.Select(item => item.Kind));
    }

    [Fact]
    public void ScanFolderMetadata_WithLowResolutionNdfMedia_SkipsThumbnailAssets()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "image-low.ndf"), CreatePngNdf(640, 360));
        File.WriteAllBytes(Path.Combine(folder, "image-full.ndf"), CreatePngNdf(1920, 1080));
        File.WriteAllBytes(Path.Combine(folder, "video-low.ndf"), CreateMp4Ndf(640, 360));
        File.WriteAllBytes(Path.Combine(folder, "video-full.ndf"), CreateMp4Ndf(1920, 1080));

        IReadOnlyList<ImageMetadata> media = ImageLibrary.ScanFolderMetadata(folder, PlaybackOrder.NameAsc);

        Assert.Equal(["image-full.ndf", "video-full.ndf"], media.Select(item => item.FileName));
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

    private static byte[] CreatePngNdf(int width, int height)
    {
        byte[] bytes = new byte[24];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        WriteUInt32(bytes, 16, width);
        WriteUInt32(bytes, 20, height);
        return bytes;
    }

    private static byte[] CreateMp4Ndf(int width, int height)
    {
        byte[] trackHeader = [0x00, 0x00, 0x00, 0x00, .. CreateFixed16(width), .. CreateFixed16(height)];
        return [0x00, 0x00, .. CreateBox("ftyp", new byte[16]), .. CreateBox("moov", CreateBox("trak", CreateBox("tkhd", trackHeader)))];
    }

    private static byte[] CreateBox(string type, byte[] payload)
    {
        byte[] bytes = new byte[payload.Length + 8];
        WriteUInt32(bytes, 0, bytes.Length);
        bytes[4] = (byte)type[0];
        bytes[5] = (byte)type[1];
        bytes[6] = (byte)type[2];
        bytes[7] = (byte)type[3];
        payload.CopyTo(bytes, 8);
        return bytes;
    }

    private static byte[] CreateFixed16(int value)
    {
        byte[] bytes = new byte[4];
        WriteUInt32(bytes, 0, value << 16);
        return bytes;
    }

    private static void WriteUInt32(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)((value >> 24) & 0xFF);
        bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
        bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 3] = (byte)(value & 0xFF);
    }
}
