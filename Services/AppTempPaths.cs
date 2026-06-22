namespace SlideShowWallpaper.Services;

public static class AppTempPaths
{
    private const long MaxCacheBytes = 512L * 1024 * 1024;
    private static readonly TimeSpan MaxTempAge = TimeSpan.FromDays(2);

    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "SlideShowWallpaper");

    public static string NdfCache { get; } = Path.Combine(Root, "ndf");

    public static string ThumbnailCache { get; } = Path.Combine(Root, "thumbnails");

    public static string ThumbnailMedia { get; } = Path.Combine(Root, "thumbnail-media");

    public static void Cleanup()
    {
        DateTime cutoffUtc = DateTime.UtcNow - MaxTempAge;
        DeleteOldFiles(NdfCache, cutoffUtc);
        DeleteOldFiles(ThumbnailMedia, cutoffUtc);
        TrimDirectory(NdfCache, MaxCacheBytes);
        TrimDirectory(ThumbnailCache, MaxCacheBytes);
        TrimDirectory(ThumbnailMedia, MaxCacheBytes / 2);
    }

    public static bool HasAvailableBytes(string path, long requiredBytes)
    {
        string root = Path.GetPathRoot(Path.GetFullPath(path)) ?? Path.GetTempPath();
        var drive = new DriveInfo(root);
        return drive.AvailableFreeSpace > requiredBytes;
    }

    private static void DeleteOldFiles(string root, DateTime cutoffUtc)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            TryDeleteOldFile(file, cutoffUtc);
        }

        DeleteEmptyDirectories(root);
    }

    private static void TryDeleteOldFile(string file, DateTime cutoffUtc)
    {
        try
        {
            if (File.GetLastWriteTimeUtc(file) < cutoffUtc)
            {
                File.Delete(file);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TrimDirectory(string root, long maxBytes)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        FileInfo[] files = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderBy(info => info.LastWriteTimeUtc)
            .ToArray();
        long totalBytes = files.Sum(info => info.Length);
        foreach (FileInfo file in files)
        {
            if (totalBytes <= maxBytes)
            {
                break;
            }

            try
            {
                long length = file.Length;
                file.Delete();
                totalBytes -= length;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        DeleteEmptyDirectories(root);
    }

    private static void DeleteEmptyDirectories(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
        {
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
}
