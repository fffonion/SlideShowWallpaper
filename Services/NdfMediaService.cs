using System.Security.Cryptography;
using System.Text;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class NdfMediaService
{
    private const string Extension = ".ndf";
    private const int Mp4Offset = 2;
    private const int ProbeLength = 12;
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
        }

        ReadOnlySpan<byte> bytes = header[..bytesRead];
        if (bytes.StartsWith(PngSignature))
        {
            info = new NdfMediaInfo(MediaKind.Image, 0, ".png");
            return true;
        }

        if (bytes.StartsWith(JpegSignature))
        {
            info = new NdfMediaInfo(MediaKind.Image, 0, ".jpg");
            return true;
        }

        if (bytes.Length >= 10 && bytes[6..10].SequenceEqual(FtypSignature))
        {
            info = new NdfMediaInfo(MediaKind.Video, Mp4Offset, ".mp4");
            return true;
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
            await using (FileStream source = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (info.Offset > 0)
                {
                    source.Seek(info.Offset, SeekOrigin.Begin);
                }

                await using FileStream destination = File.Open(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(destination, cancellationToken);
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

    private static bool IsNdfPath(string path)
    {
        return string.Equals(Path.GetExtension(path), Extension, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetMaterializedPath(string path, NdfMediaInfo info, string cacheRoot)
    {
        var file = new FileInfo(path);
        string input = string.Join('\0', file.FullName, file.LastWriteTimeUtc.Ticks.ToString(), file.Length.ToString(), info.Offset.ToString(), info.Extension);
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
        return Path.Combine(cacheRoot, hash[..2], $"{hash}{info.Extension}");
    }
}

public readonly record struct NdfMediaInfo(MediaKind Kind, int Offset, string Extension);
