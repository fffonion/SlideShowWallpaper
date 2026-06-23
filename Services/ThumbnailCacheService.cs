using System.Security.Cryptography;
using System.Text;
using SlideShowWallpaper.Models;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace SlideShowWallpaper.Services;

public sealed class ThumbnailCacheService
{
    private const uint DefaultMaxPixelSize = 320;
    private const string ThumbnailExtension = ".jpg";
    private static readonly SemaphoreSlim VideoThumbnailGate = new(1, 1);
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
        try
        {
            await CreateImageThumbnailFromDecodedStreamAsync(sourcePath, thumbnailPath, maxPixelSize, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AppLog.Write(exception);
            DeleteIfExists(thumbnailPath);
            await CreateImageThumbnailFromShellAsync(sourcePath, thumbnailPath, maxPixelSize, cancellationToken);
        }
    }

    private static async Task CreateImageThumbnailFromDecodedStreamAsync(string sourcePath, string thumbnailPath, uint maxPixelSize, CancellationToken cancellationToken)
    {
        using IRandomAccessStream sourceStream = await NdfMediaService.OpenContentStreamAsync(sourcePath, cancellationToken);
        await CreateThumbnailFromStreamAsync(sourceStream, thumbnailPath, maxPixelSize, cancellationToken);
    }

    private static async Task CreateImageThumbnailFromShellAsync(string sourcePath, string thumbnailPath, uint maxPixelSize, CancellationToken cancellationToken)
    {
        string? temporaryMediaPath = null;
        bool deleteTemporaryMedia = ShouldDeleteThumbnailMedia(sourcePath);
        try
        {
            StorageFile sourceFile = await NdfMediaService.GetStorageFileForThumbnailAsync(sourcePath, AppTempPaths.ThumbnailMedia, cancellationToken);
            temporaryMediaPath = deleteTemporaryMedia ? sourceFile.Path : null;
            using StorageItemThumbnail sourceStream = await sourceFile
                .GetThumbnailAsync(
                    ThumbnailMode.PicturesView,
                    maxPixelSize,
                    ThumbnailOptions.UseCurrentScale)
                .AsTask(cancellationToken);
            if (sourceStream is null || sourceStream.Size == 0)
            {
                throw new InvalidDataException($"No image thumbnail for {Path.GetFileName(sourcePath)}.");
            }

            await CreateThumbnailFromStreamAsync(sourceStream, thumbnailPath, maxPixelSize, cancellationToken);
        }
        finally
        {
            DeleteIfExists(temporaryMediaPath);
        }
    }

    private static async Task CreateVideoThumbnailAsync(string sourcePath, string thumbnailPath, uint maxPixelSize, CancellationToken cancellationToken)
    {
        await VideoThumbnailGate.WaitAsync(cancellationToken);
        try
        {
            await CreateVideoThumbnailCoreAsync(sourcePath, thumbnailPath, maxPixelSize, cancellationToken);
        }
        finally
        {
            VideoThumbnailGate.Release();
        }
    }

    private static async Task CreateVideoThumbnailCoreAsync(string sourcePath, string thumbnailPath, uint maxPixelSize, CancellationToken cancellationToken)
    {
        string? temporaryMediaPath = null;
        bool deleteTemporaryMedia = ShouldDeleteThumbnailMedia(sourcePath);
        try
        {
            StorageFile sourceFile = await NdfMediaService.GetStorageFileForThumbnailAsync(sourcePath, AppTempPaths.ThumbnailMedia, cancellationToken);
            temporaryMediaPath = deleteTemporaryMedia ? sourceFile.Path : null;
            try
            {
                await CreateSystemVideoThumbnailAsync(sourceFile, thumbnailPath, maxPixelSize, cachedOnly: true, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (exception is not InvalidDataException)
                {
                    AppLog.Write(exception);
                }

                DeleteIfExists(thumbnailPath);
                await CreateSystemVideoThumbnailAsync(sourceFile, thumbnailPath, maxPixelSize, cachedOnly: false, cancellationToken);
            }
        }
        finally
        {
            DeleteIfExists(temporaryMediaPath);
        }
    }

    private static async Task CreateSystemVideoThumbnailAsync(StorageFile sourceFile, string thumbnailPath, uint maxPixelSize, bool cachedOnly, CancellationToken cancellationToken)
    {
        ThumbnailOptions options = cachedOnly
            ? ThumbnailOptions.ReturnOnlyIfCached | ThumbnailOptions.UseCurrentScale
            : ThumbnailOptions.UseCurrentScale;
        using StorageItemThumbnail sourceStream = await sourceFile
            .GetThumbnailAsync(
                ThumbnailMode.VideosView,
                maxPixelSize,
                options)
            .AsTask(cancellationToken);
        if (sourceStream is null || sourceStream.Size == 0)
        {
            throw new InvalidDataException($"No cached system thumbnail for {sourceFile.Name}.");
        }

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
