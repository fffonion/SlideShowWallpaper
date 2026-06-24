using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class ImageDimensionReaderTests
{
    [Fact]
    public void TryRead_WithPngHeader_ReturnsDimensions()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "background.png");
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(path, CreatePngHeader(width: 320, height: 180));

        bool result = ImageDimensionReader.TryRead(path, out int width, out int height);

        Assert.True(result);
        Assert.Equal(320, width);
        Assert.Equal(180, height);
    }

    private static byte[] CreatePngHeader(int width, int height)
    {
        byte[] bytes =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        WriteBigEndian(bytes, 16, width);
        WriteBigEndian(bytes, 20, height);
        return bytes;
    }

    private static void WriteBigEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)((value >> 24) & 0xFF);
        bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
        bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
        bytes[offset + 3] = (byte)(value & 0xFF);
    }
}
