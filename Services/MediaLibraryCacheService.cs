using System.Text.Json;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class MediaLibraryCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private readonly object _sync = new();
    private readonly string _cachePath;

    public MediaLibraryCacheService()
        : this(AppTempPaths.MediaLibraryCache)
    {
    }

    public MediaLibraryCacheService(string cachePath)
    {
        _cachePath = cachePath;
    }

    public bool TryLoad(string folderPath, bool includeSubdirectories, out IReadOnlyList<ImageMetadata> media)
    {
        media = [];
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        lock (_sync)
        {
            CacheFile cache = LoadCache();
            string key = CreateKey(folderPath, includeSubdirectories);
            if (!cache.Entries.TryGetValue(key, out CacheEntry? entry))
            {
                return false;
            }

            media = entry.Media
                .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                .ToArray();
            return true;
        }
    }

    public void Save(string folderPath, bool includeSubdirectories, IReadOnlyList<ImageMetadata> media)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        lock (_sync)
        {
            CacheFile cache = LoadCache();
            cache.Entries[CreateKey(folderPath, includeSubdirectories)] = new CacheEntry
            {
                FolderPath = NormalizeFolder(folderPath),
                IncludeSubdirectories = includeSubdirectories,
                SavedUtc = DateTime.UtcNow,
                Media = media.ToArray(),
            };
            SaveCache(cache);
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            try
            {
                if (File.Exists(_cachePath))
                {
                    File.Delete(_cachePath);
                }
            }
            catch (IOException exception)
            {
                AppLog.Write(exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                AppLog.Write(exception);
            }
        }
    }

    private CacheFile LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return new CacheFile();
            }

            string json = File.ReadAllText(_cachePath);
            return JsonSerializer.Deserialize<CacheFile>(json, JsonOptions) ?? new CacheFile();
        }
        catch (JsonException exception)
        {
            AppLog.Write(exception);
            return new CacheFile();
        }
        catch (IOException exception)
        {
            AppLog.Write(exception);
            return new CacheFile();
        }
        catch (UnauthorizedAccessException exception)
        {
            AppLog.Write(exception);
            return new CacheFile();
        }
    }

    private void SaveCache(CacheFile cache)
    {
        try
        {
            string? folder = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            File.WriteAllText(_cachePath, JsonSerializer.Serialize(cache, JsonOptions));
        }
        catch (IOException exception)
        {
            AppLog.Write(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            AppLog.Write(exception);
        }
    }

    private static string CreateKey(string folderPath, bool includeSubdirectories)
    {
        return $"{includeSubdirectories}|{NormalizeFolder(folderPath)}";
    }

    private static string NormalizeFolder(string folderPath)
    {
        return Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed class CacheFile
    {
        public Dictionary<string, CacheEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class CacheEntry
    {
        public string FolderPath { get; set; } = string.Empty;

        public bool IncludeSubdirectories { get; set; }

        public DateTime SavedUtc { get; set; }

        public ImageMetadata[] Media { get; set; } = [];
    }
}
