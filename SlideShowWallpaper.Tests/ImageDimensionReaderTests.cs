using SlideShowWallpaper.Services;
using Windows.Graphics.Imaging;

namespace SlideShowWallpaper.Tests;

public sealed class ImageDimensionReaderTests
{
    [Fact]
    public async Task ReadAsync_WithSystemCodecFormat_ReadsDimensionsByContent()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = await ImageTestFiles.CreateAsync(folder, "image.heic", BitmapEncoder.TiffEncoderId);

        Assert.False(ImageDimensionReader.TryRead(path, out _, out _));
        (int width, int height) = await ImageDimensionReader.ReadAsync(path);

        Assert.Equal(40, width);
        Assert.Equal(80, height);
    }

    [Theory]
    [InlineData(1, 40, 80)]
    [InlineData(2, 40, 80)]
    [InlineData(3, 40, 80)]
    [InlineData(4, 40, 80)]
    [InlineData(5, 80, 40)]
    [InlineData(6, 80, 40)]
    [InlineData(7, 80, 40)]
    [InlineData(8, 80, 40)]
    public async Task ReadAsync_WithExifOrientation_ReturnsDisplayDimensions(int orientation, int expectedWidth, int expectedHeight)
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        string path = await ImageTestFiles.CreateAsync(folder, "image.jpg", BitmapEncoder.JpegEncoderId, (ushort)orientation);

        (int width, int height) = await ImageDimensionReader.ReadAsync(path);

        Assert.Equal(expectedWidth, width);
        Assert.Equal(expectedHeight, height);
    }

    [Fact]
    public async Task ReadAsync_WithMissingFile_ReturnsUnknownDimensions()
    {
        (int width, int height) = await ImageDimensionReader.ReadAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }

    [Fact]
    public async Task ReadAsync_WithInvalidData_ReturnsUnknownDimensions()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "invalid.png");
        await File.WriteAllBytesAsync(path, [0, 0]);

        Assert.Equal((0, 0), await ImageDimensionReader.ReadAsync(path));
    }

    [Fact]
    public async Task ReadAsync_WithCanceledToken_ThrowsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ImageDimensionReader.ReadAsync("image.heic", cancellation.Token));
    }

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
