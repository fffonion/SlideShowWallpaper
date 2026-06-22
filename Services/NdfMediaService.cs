using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using SlideShowWallpaper.Models;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SlideShowWallpaper.Services;

public static class NdfMediaService
{
    private const string Extension = ".ndf";
    private const int JpegStartOfImageLength = 2;
    private const int MaxDimensionProbeBytes = 1024 * 1024;
    private const int Mp4Offset = 2;
    private const int PngDimensionLength = 24;
    private const int ProbeLength = 24;
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] FtypSignature = [0x66, 0x74, 0x79, 0x70];

    public static bool TryGetMediaInfo(string path, out NdfMediaInfo info)
    {
        info = default;
        if (!IsNdfPath(path) || !File.Exists(path))
        {
            return false;
        }

        Span<byte> header = stackalloc byte[ProbeLength];
        int bytesRead;
        using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            bytesRead = stream.Read(header);

            ReadOnlySpan<byte> bytes = header[..bytesRead];
            if (bytes.StartsWith(PngSignature))
            {
                (int width, int height) = TryReadPngDimensions(bytes, out NdfMediaDimensions dimensions)
                    ? (dimensions.Width, dimensions.Height)
                    : (0, 0);
                info = new NdfMediaInfo(MediaKind.Image, 0, ".png", width, height);
                return true;
            }

            if (bytes.StartsWith(JpegSignature))
            {
                (int width, int height) = TryReadJpegDimensions(stream, out NdfMediaDimensions dimensions)
                    ? (dimensions.Width, dimensions.Height)
                    : (0, 0);
                info = new NdfMediaInfo(MediaKind.Image, 0, ".jpg", width, height);
                return true;
            }

            if (bytes.Length >= 10 && bytes[6..10].SequenceEqual(FtypSignature))
            {
                (int width, int height) = TryReadMp4Dimensions(stream, Mp4Offset, out NdfMediaDimensions dimensions)
                    ? (dimensions.Width, dimensions.Height)
                    : (0, 0);
                info = new NdfMediaInfo(MediaKind.Video, Mp4Offset, ".mp4", width, height);
                return true;
            }
        }

        return false;
    }

    public static Task<string> MaterializeForPlaybackAsync(string path, CancellationToken cancellationToken = default)
    {
        return MaterializeForPlaybackAsync(path, Path.Combine(Path.GetTempPath(), "SlideShowWallpaper", "ndf"), cancellationToken);
    }

    public static async Task<string> MaterializeForPlaybackAsync(string path, string cacheRoot, CancellationToken cancellationToken = default)
    {
        if (!TryGetMediaInfo(path, out NdfMediaInfo info))
        {
            return path;
        }

        string outputPath = GetMaterializedPath(path, info, cacheRoot);
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        string temporaryPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream destination = File.Open(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await CopyNdfContentAsync(path, info, destination, cancellationToken);
            }

            try
            {
                File.Move(temporaryPath, outputPath);
            }
            catch (IOException) when (File.Exists(outputPath))
            {
                File.Delete(temporaryPath);
            }

            return outputPath;
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public static async Task<IRandomAccessStream> OpenContentStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!TryGetMediaInfo(path, out NdfMediaInfo info))
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(path).AsTask(cancellationToken);
            return await file.OpenReadAsync().AsTask(cancellationToken);
        }

        var stream = new InMemoryRandomAccessStream();
        using (Stream output = stream.AsStreamForWrite())
        {
            await CopyNdfContentAsync(path, info, output, cancellationToken);
            await output.FlushAsync(cancellationToken);
        }

        stream.Seek(0);
        return stream;
    }

    public static async Task<StorageFile> GetStorageFileForThumbnailAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!TryGetMediaInfo(path, out NdfMediaInfo info))
        {
            return await StorageFile.GetFileFromPathAsync(path).AsTask(cancellationToken);
        }

        return await StorageFile.CreateStreamedFileAsync(
            $"{Path.GetFileNameWithoutExtension(path)}{info.Extension}",
            async request =>
            {
                using (request)
                {
                    using Stream output = request.AsStreamForWrite();
                    await CopyNdfContentAsync(path, info, output, cancellationToken);
                    await output.FlushAsync(cancellationToken);
                }
            },
            null).AsTask(cancellationToken);
    }

    private static bool IsNdfPath(string path)
    {
        return string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadPngDimensions(ReadOnlySpan<byte> bytes, out NdfMediaDimensions dimensions)
    {
        dimensions = default;
        if (bytes.Length < PngDimensionLength)
        {
            return false;
        }

        dimensions = new NdfMediaDimensions(
            (int)BinaryPrimitives.ReadUInt32BigEndian(bytes[16..20]),
            (int)BinaryPrimitives.ReadUInt32BigEndian(bytes[20..24]));
        return dimensions.Width > 0 && dimensions.Height > 0;
    }

    private static bool TryReadJpegDimensions(Stream stream, out NdfMediaDimensions dimensions)
    {
        dimensions = default;
        stream.Seek(JpegStartOfImageLength, SeekOrigin.Begin);
        long probeEnd = Math.Min(stream.Length, MaxDimensionProbeBytes);
        Span<byte> marker = stackalloc byte[2];
        Span<byte> lengthBytes = stackalloc byte[2];
        Span<byte> sizeBytes = stackalloc byte[5];

        while (stream.Position + 4 <= probeEnd)
        {
            if (stream.Read(marker) != marker.Length || marker[0] != 0xFF)
            {
                return false;
            }

            while (marker[1] == 0xFF)
            {
                int next = stream.ReadByte();
                if (next < 0)
                {
                    return false;
                }

                marker[1] = (byte)next;
            }

            if (marker[1] is 0xD9 or 0xDA)
            {
                return false;
            }

            if (stream.Read(lengthBytes) != lengthBytes.Length)
            {
                return false;
            }

            int segmentLength = BinaryPrimitives.ReadUInt16BigEndian(lengthBytes);
            if (segmentLength < 2 || stream.Position + segmentLength - 2 > probeEnd)
            {
                return false;
            }

            if (IsJpegStartOfFrame(marker[1]))
            {
                if (segmentLength < 7 || stream.Read(sizeBytes) != sizeBytes.Length)
                {
                    return false;
                }

                dimensions = new NdfMediaDimensions(
                    BinaryPrimitives.ReadUInt16BigEndian(sizeBytes[3..5]),
                    BinaryPrimitives.ReadUInt16BigEndian(sizeBytes[1..3]));
                return dimensions.Width > 0 && dimensions.Height > 0;
            }

            stream.Seek(segmentLength - 2, SeekOrigin.Current);
        }

        return false;
    }

    private static bool TryReadMp4Dimensions(Stream stream, long offset, out NdfMediaDimensions dimensions)
    {
        dimensions = default;
        long probeEnd = Math.Min(stream.Length, offset + MaxDimensionProbeBytes);
        return TryReadMp4Boxes(stream, offset, probeEnd, out dimensions);
    }

    private static bool TryReadMp4Boxes(Stream stream, long start, long end, out NdfMediaDimensions dimensions)
    {
        dimensions = default;
        Span<byte> header = stackalloc byte[8];
        Span<byte> largeSizeBytes = stackalloc byte[8];
        long position = start;
        while (position + header.Length <= end)
        {
            stream.Position = position;
            if (stream.Read(header) != header.Length)
            {
                return false;
            }

            ulong size = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
            string type = Encoding.ASCII.GetString(header[4..8]);
            int headerLength = 8;
            if (size == 1)
            {
                if (stream.Read(largeSizeBytes) != largeSizeBytes.Length)
                {
                    return false;
                }

                size = BinaryPrimitives.ReadUInt64BigEndian(largeSizeBytes);
                headerLength = 16;
            }
            else if (size == 0)
            {
                size = (ulong)(end - position);
            }

            if (size < (ulong)headerLength)
            {
                return false;
            }

            long boxEnd = position + (long)size;
            if (boxEnd > end)
            {
                boxEnd = end;
            }

            if (type is "moov" or "trak")
            {
                if (TryReadMp4Boxes(stream, position + headerLength, boxEnd, out dimensions))
                {
                    return true;
                }
            }
            else if (type == "tkhd" && TryReadTrackHeaderDimensions(stream, position + headerLength, boxEnd, out dimensions))
            {
                return true;
            }

            position += (long)size;
        }

        return false;
    }

    private static bool TryReadTrackHeaderDimensions(Stream stream, long start, long end, out NdfMediaDimensions dimensions)
    {
        dimensions = default;
        if (end - start < 12)
        {
            return false;
        }

        Span<byte> dimensionsBytes = stackalloc byte[8];
        stream.Position = end - dimensionsBytes.Length;
        if (stream.Read(dimensionsBytes) != dimensionsBytes.Length)
        {
            return false;
        }

        dimensions = new NdfMediaDimensions(
            (int)(BinaryPrimitives.ReadUInt32BigEndian(dimensionsBytes[..4]) >> 16),
            (int)(BinaryPrimitives.ReadUInt32BigEndian(dimensionsBytes[4..]) >> 16));
        return dimensions.Width > 0 && dimensions.Height > 0;
    }

    private static bool IsJpegStartOfFrame(byte marker)
    {
        return marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;
    }

    private static string GetMaterializedPath(string path, NdfMediaInfo info, string cacheRoot)
    {
        var file = new FileInfo(path);
        string input = string.Join('\0', file.FullName, file.LastWriteTimeUtc.Ticks.ToString(), file.Length.ToString(), info.Offset.ToString(), info.Extension);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
        return Path.Combine(cacheRoot, hash[..2], $"{hash}{info.Extension}");
    }

    private static async Task CopyNdfContentAsync(string path, NdfMediaInfo info, Stream destination, CancellationToken cancellationToken)
    {
        await using FileStream source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (info.Offset > 0)
        {
            source.Seek(info.Offset, SeekOrigin.Begin);
        }

        await source.CopyToAsync(destination, cancellationToken);
    }
}

public readonly record struct NdfMediaInfo(MediaKind Kind, int Offset, string Extension, int Width = 0, int Height = 0);

internal readonly record struct NdfMediaDimensions(int Width, int Height);
