using System.Security.Cryptography;
using System.Text;
using SlideShowWallpaper.Models;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SlideShowWallpaper.Services;

public sealed class ThumbnailCacheService
{
    private const uint DefaultMaxPixelSize = 320;
    private readonly string _cacheRoot;
    private readonly Func<string, string, uint, CancellationToken, Task> _imageThumbnailWriter;
    private readonly Func<string, string, uint, CancellationToken, Task> _videoThumbnailWriter;
    private readonly Func<string, string, CancellationToken, Task<string>> _mediaMaterializer;

    public ThumbnailCacheService()
        : this(Path.Combine(Path.GetTempPath(), "SlideShowWallpaper", "thumbnails"))
    {
    }

    public ThumbnailCacheService(string cacheRoot)
        : this(cacheRoot, CreateImageThumbnailAsync, CreateVideoThumbnailAsync, NdfMediaService.MaterializeForPlaybackAsync)
    {
    }

    internal ThumbnailCacheService(
        string cacheRoot,
        Func<string, string, uint, CancellationToken, Task> imageThumbnailWriter,
        Func<string, string, uint, CancellationToken, Task> videoThumbnailWriter,
        Func<string, string, CancellationToken, Task<string>> mediaMaterializer)
    {
        _cacheRoot = cacheRoot;
        _imageThumbnailWriter = imageThumbnailWriter;
        _videoThumbnailWriter = videoThumbnailWriter;
        _mediaMaterializer = mediaMaterializer;
    }

    public string GetThumbnailPath(ImageMetadata metadata)
    {
        string input = string.Join('\0', metadata.Path, metadata.ModifiedUtc.Ticks.ToString(), metadata.Length.ToString());
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
        return Path.Combine(_cacheRoot, hash[..2], $"{hash}.png");
    }

    public async Task<string> GetOrCreateThumbnailAsync(ImageMetadata metadata, CancellationToken cancellationToken = default)
    {
        string thumbnailPath = GetThumbnailPath(metadata);
        if (File.Exists(thumbnailPath))
        {
            return thumbnailPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        string temporaryPath = $"{thumbnailPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            string materializedPath = await _mediaMaterializer(metadata.Path, Path.Combine(_cacheRoot, "media"), cancellationToken);
            if (metadata.Kind == MediaKind.Video)
            {
                await _videoThumbnailWriter(materializedPath, temporaryPath, DefaultMaxPixelSize, cancellationToken);
            }
            else
            {
                await _imageThumbnailWriter(materializedPath, temporaryPath, DefaultMaxPixelSize, cancellationToken);
            }

            if (File.Exists(thumbnailPath))
            {
                File.Delete(temporaryPath);
            }
            else
            {
                File.Move(temporaryPath, thumbnailPath);
            }
        }
        catch
        {
            DeleteIfExists(temporaryPath);
            throw;
        }

        return thumbnailPath;
    }

    private static async Task CreateImageThumbnailAsync(string sourcePath, string thumbnailPath, uint maxPixelSize, CancellationToken cancellationToken)
    {
        StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath).AsTask(cancellationToken);
        using IRandomAccessStream sourceStream = await sourceFile.OpenReadAsync().AsTask(cancellationToken);
        await CreateThumbnailFromStreamAsync(sourceStream, thumbnailPath, maxPixelSize, cancellationToken);
    }

    private static async Task CreateVideoThumbnailAsync(string sourcePath, string thumbnailPath, uint maxPixelSize, CancellationToken cancellationToken)
    {
        StorageFile sourceFile = await StorageFile.GetFileFromPathAsync(sourcePath).AsTask(cancellationToken);
        MediaClip clip = await MediaClip.CreateFromFileAsync(sourceFile).AsTask(cancellationToken);
        var composition = new MediaComposition();
        composition.Clips.Add(clip);
        var properties = clip.GetVideoEncodingProperties();
        (uint width, uint height) = GetScaledSize(properties.Width, properties.Height, maxPixelSize);
        using IRandomAccessStream sourceStream = await composition.GetThumbnailAsync(TimeSpan.Zero, (int)width, (int)height, VideoFramePrecision.NearestFrame).AsTask(cancellationToken);
        await CreateThumbnailFromStreamAsync(sourceStream, thumbnailPath, maxPixelSize, cancellationToken);
    }

    private static async Task CreateThumbnailFromStreamAsync(IRandomAccessStream sourceStream, string thumbnailPath, uint maxPixelSize, CancellationToken cancellationToken)
    {
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(sourceStream).AsTask(cancellationToken);
        (uint width, uint height) = GetScaledSize(decoder.PixelWidth, decoder.PixelHeight, maxPixelSize);
        var transform = new BitmapTransform
        {
            ScaledWidth = width,
            ScaledHeight = height,
            InterpolationMode = BitmapInterpolationMode.Fant,
        };

        PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb).AsTask(cancellationToken);

        using FileStream fileStream = File.Open(thumbnailPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using IRandomAccessStream thumbnailStream = fileStream.AsRandomAccessStream();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, thumbnailStream).AsTask(cancellationToken);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, width, height, decoder.DpiX, decoder.DpiY, pixelData.DetachPixelData());
        await encoder.FlushAsync().AsTask(cancellationToken);
    }

    private static (uint Width, uint Height) GetScaledSize(uint sourceWidth, uint sourceHeight, uint maxPixelSize)
    {
        if (sourceWidth == 0 || sourceHeight == 0)
        {
            return (maxPixelSize, maxPixelSize);
        }

        double scale = Math.Min(1.0, maxPixelSize / (double)Math.Max(sourceWidth, sourceHeight));
        return ((uint)Math.Max(1, Math.Round(sourceWidth * scale)), (uint)Math.Max(1, Math.Round(sourceHeight * scale)));
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
