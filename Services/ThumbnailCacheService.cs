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
    private const string ThumbnailExtension = ".jpg";
    private static readonly TimeSpan PreferredVideoThumbnailTime = TimeSpan.FromSeconds(3);
    private readonly string _cacheRoot;
    private readonly Func<ImageMetadata, string, uint, CancellationToken, Task> _thumbnailWriter;

    public ThumbnailCacheService()
        : this(AppTempPaths.ThumbnailCache)
    {
    }

    public ThumbnailCacheService(string cacheRoot)
        : this(cacheRoot, CreateThumbnailAsync)
    {
    }

    internal ThumbnailCacheService(
        string cacheRoot,
        Func<ImageMetadata, string, uint, CancellationToken, Task> thumbnailWriter)
    {
        _cacheRoot = cacheRoot;
        _thumbnailWriter = thumbnailWriter;
    }

    public Task<long> GetCacheSizeBytesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => GetCacheSizeBytes(cancellationToken), cancellationToken);
    }

    public Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ClearCache(cancellationToken), cancellationToken);
    }

    public string GetThumbnailPath(ImageMetadata metadata)
    {
        string input = string.Join('\0', metadata.Path, metadata.ModifiedUtc.Ticks.ToString(), metadata.Length.ToString());
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
        return Path.Combine(_cacheRoot, hash[..2], $"{hash}{ThumbnailExtension}");
    }

    public async Task<string> GetOrCreateThumbnailAsync(ImageMetadata metadata, CancellationToken cancellationToken = default)
    {
        string thumbnailPath = GetThumbnailPath(metadata);
        if (File.Exists(thumbnailPath))
        {
            File.SetLastWriteTimeUtc(thumbnailPath, DateTime.UtcNow);
            return thumbnailPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
        string temporaryPath = $"{thumbnailPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await _thumbnailWriter(metadata, temporaryPath, DefaultMaxPixelSize, cancellationToken);

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

    public async Task<string> CreateTemporaryThumbnailAsync(ImageMetadata metadata, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppTempPaths.TransientThumbnails);
        string thumbnailPath = Path.Combine(AppTempPaths.TransientThumbnails, $"{Guid.NewGuid():N}{ThumbnailExtension}");
        try
        {
            await _thumbnailWriter(metadata, thumbnailPath, DefaultMaxPixelSize, cancellationToken);
        }
        catch
        {
            DeleteIfExists(thumbnailPath);
            throw;
        }

        return thumbnailPath;
    }

    private static async Task CreateThumbnailAsync(ImageMetadata metadata, string thumbnailPath, uint maxPixelSize, CancellationToken cancellationToken)
    {
        if (metadata.Kind == MediaKind.Video)
        {
            await CreateVideoThumbnailAsync(metadata.Path, thumbnailPath, maxPixelSize, cancellationToken);
        }
        else
        {
            await CreateImageThumbnailAsync(metadata.Path, thumbnailPath, maxPixelSize, cancellationToken);
        }
    }

    private static async Task CreateImageThumbnailAsync(string sourcePath, string thumbnailPath, uint maxPixelSize, CancellationToken cancellationToken)
    {
        using IRandomAccessStream sourceStream = await NdfMediaService.OpenContentStreamAsync(sourcePath, cancellationToken);
        await CreateThumbnailFromStreamAsync(sourceStream, thumbnailPath, maxPixelSize, cancellationToken);
    }

    private static async Task CreateVideoThumbnailAsync(string sourcePath, string thumbnailPath, uint maxPixelSize, CancellationToken cancellationToken)
    {
        string? temporaryMediaPath = null;
        bool deleteTemporaryMedia = ShouldDeleteThumbnailMedia(sourcePath);
        try
        {
            StorageFile sourceFile = await NdfMediaService.GetStorageFileForThumbnailAsync(sourcePath, AppTempPaths.ThumbnailMedia, cancellationToken);
            temporaryMediaPath = deleteTemporaryMedia ? sourceFile.Path : null;
            MediaClip clip = await MediaClip.CreateFromFileAsync(sourceFile).AsTask(cancellationToken);
            var composition = new MediaComposition();
            composition.Clips.Add(clip);
            var properties = clip.GetVideoEncodingProperties();
            (uint width, uint height) = GetScaledSize(properties.Width, properties.Height, maxPixelSize);
            TimeSpan thumbnailTime = GetVideoThumbnailTime(composition.Duration);
            using IRandomAccessStream sourceStream = await GetVideoThumbnailStreamAsync(composition, thumbnailTime, width, height, cancellationToken);
            await CreateThumbnailFromStreamAsync(sourceStream, thumbnailPath, maxPixelSize, cancellationToken);
        }
        finally
        {
            DeleteIfExists(temporaryMediaPath);
        }
    }

    private static async Task<IRandomAccessStream> GetVideoThumbnailStreamAsync(
        MediaComposition composition,
        TimeSpan thumbnailTime,
        uint width,
        uint height,
        CancellationToken cancellationToken)
    {
        try
        {
            return await composition.GetThumbnailAsync(thumbnailTime, (int)width, (int)height, VideoFramePrecision.NearestKeyFrame).AsTask(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return await composition.GetThumbnailAsync(thumbnailTime, (int)width, (int)height, VideoFramePrecision.NearestFrame).AsTask(cancellationToken);
        }
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
            BitmapAlphaMode.Ignore,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb).AsTask(cancellationToken);

        using FileStream fileStream = File.Open(thumbnailPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using IRandomAccessStream thumbnailStream = fileStream.AsRandomAccessStream();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, thumbnailStream).AsTask(cancellationToken);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, width, height, decoder.DpiX, decoder.DpiY, pixelData.DetachPixelData());
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

    internal static TimeSpan GetVideoThumbnailTime(TimeSpan duration)
    {
        return duration > PreferredVideoThumbnailTime ? PreferredVideoThumbnailTime : TimeSpan.Zero;
    }

    internal static bool ShouldDeleteThumbnailMedia(string sourcePath)
    {
        return NdfMediaService.TryGetMediaInfo(sourcePath, out _);
    }

    private long GetCacheSizeBytes(CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        foreach (string file in EnumerateCacheFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                totalBytes += new FileInfo(file).Length;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return totalBytes;
    }

    private void ClearCache(CancellationToken cancellationToken)
    {
        foreach (string file in EnumerateCacheFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteIfExists(file);
        }

        DeleteEmptyCacheDirectories(cancellationToken);
    }

    private IEnumerable<string> EnumerateCacheFiles()
    {
        if (!IsUsableCacheRoot())
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = System.IO.FileAttributes.ReparsePoint,
        };

        try
        {
            return Directory.EnumerateFiles(_cacheRoot, "*", options).ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void DeleteEmptyCacheDirectories(CancellationToken cancellationToken)
    {
        if (!IsUsableCacheRoot())
        {
            return;
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = System.IO.FileAttributes.ReparsePoint,
        };

        foreach (string directory in Directory.EnumerateDirectories(_cacheRoot, "*", options).OrderByDescending(path => path.Length))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private bool IsUsableCacheRoot()
    {
        if (!Directory.Exists(_cacheRoot))
        {
            return false;
        }

        try
        {
            return !new DirectoryInfo(_cacheRoot).Attributes.HasFlag(System.IO.FileAttributes.ReparsePoint);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
