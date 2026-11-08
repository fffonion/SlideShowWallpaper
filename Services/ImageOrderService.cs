using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class ImageOrderService
{
    private readonly object _sync = new();
    private readonly Random _random;
    private readonly Dictionary<string, Task<IReadOnlyList<ImageMetadata>>> _tasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<IReadOnlyList<ImageMetadata>>> _dimensionRepairTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly MediaLibraryCacheService _mediaCache;

    public ImageOrderService()
        : this(Random.Shared)
    {
    }

    public ImageOrderService(Random random)
        : this(random, new MediaLibraryCacheService())
    {
    }

    public ImageOrderService(Random random, MediaLibraryCacheService mediaCache)
    {
        _random = random;
        _mediaCache = mediaCache;
    }

    public async Task<IReadOnlyList<ImageMetadata>> GetOrLoadOrderedImagesAsync(string folderPath, PlaybackOrder order, CancellationToken cancellationToken)
    {
        return await GetOrLoadOrderedImagesAsync(folderPath, order, PlaybackMediaFilter.ImagesAndVideos, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageMetadata>> GetOrLoadOrderedImagesAsync(string folderPath, PlaybackOrder order, PlaybackMediaFilter mediaFilter, CancellationToken cancellationToken)
    {
        return await GetOrLoadOrderedImagesAsync(folderPath, order, mediaFilter, includeSubdirectories: false, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageMetadata>> GetOrLoadOrderedImagesAsync(
        string folderPath,
        PlaybackOrder order,
        PlaybackMediaFilter mediaFilter,
        bool includeSubdirectories,
        CancellationToken cancellationToken)
    {
        ImageOrderLoadResult result = await GetOrLoadOrderedImagesWithStatusAsync(folderPath, order, mediaFilter, includeSubdirectories, cancellationToken);
        return result.Images;
    }

    public async Task<ImageOrderLoadResult> GetOrLoadOrderedImagesWithStatusAsync(
        string folderPath,
        PlaybackOrder order,
        PlaybackMediaFilter mediaFilter,
        bool includeSubdirectories,
        CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<ImageMetadata>>? existing = TryGetExistingTask(folderPath, order, mediaFilter, includeSubdirectories);
        if (existing is not null)
        {
            return new ImageOrderLoadResult(await existing.WaitAsync(cancellationToken), LoadedFromCache: false);
        }

        if (_mediaCache.TryLoad(folderPath, includeSubdirectories, out IReadOnlyList<ImageMetadata> cachedMedia, out bool hasCurrentDimensions))
        {
            if (!hasCurrentDimensions)
            {
                cachedMedia = await GetOrRepairCachedDimensionsAsync(folderPath, includeSubdirectories, cachedMedia).WaitAsync(cancellationToken);
            }

            return new ImageOrderLoadResult(ApplyOrderAndFilter(cachedMedia, order, mediaFilter), LoadedFromCache: true);
        }

        Task<IReadOnlyList<ImageMetadata>> task = GetOrCreateTask(folderPath, order, mediaFilter, includeSubdirectories, reload: false);
        return new ImageOrderLoadResult(await task.WaitAsync(cancellationToken), LoadedFromCache: false);
    }

    public async Task<IReadOnlyList<ImageMetadata>> ReloadOrderedImagesAsync(string folderPath, PlaybackOrder order, CancellationToken cancellationToken)
    {
        return await ReloadOrderedImagesAsync(folderPath, order, PlaybackMediaFilter.ImagesAndVideos, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageMetadata>> ReloadOrderedImagesAsync(string folderPath, PlaybackOrder order, PlaybackMediaFilter mediaFilter, CancellationToken cancellationToken)
    {
        return await ReloadOrderedImagesAsync(folderPath, order, mediaFilter, includeSubdirectories: false, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageMetadata>> ReloadOrderedImagesAsync(
        string folderPath,
        PlaybackOrder order,
        PlaybackMediaFilter mediaFilter,
        bool includeSubdirectories,
        CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<ImageMetadata>> task = GetOrCreateTask(folderPath, order, mediaFilter, includeSubdirectories, reload: true);
        return await task.WaitAsync(cancellationToken);
    }

    public void ClearCache()
    {
        lock (_sync)
        {
            _tasks.Clear();
            _dimensionRepairTasks.Clear();
        }

        _mediaCache.Clear();
    }

    private Task<IReadOnlyList<ImageMetadata>>? TryGetExistingTask(
        string folderPath,
        PlaybackOrder order,
        PlaybackMediaFilter mediaFilter,
        bool includeSubdirectories)
    {
        string key = CreateKey(folderPath, order, mediaFilter, includeSubdirectories);
        lock (_sync)
        {
            return _tasks.TryGetValue(key, out Task<IReadOnlyList<ImageMetadata>>? existing)
                && !existing.IsFaulted
                && !existing.IsCanceled
                ? existing
                : null;
        }
    }

    private Task<IReadOnlyList<ImageMetadata>> GetOrCreateTask(
        string folderPath,
        PlaybackOrder order,
        PlaybackMediaFilter mediaFilter,
        bool includeSubdirectories,
        bool reload)
    {
        string key = CreateKey(folderPath, order, mediaFilter, includeSubdirectories);
        lock (_sync)
        {
            if (!reload
                && _tasks.TryGetValue(key, out Task<IReadOnlyList<ImageMetadata>>? existing)
                && !existing.IsFaulted
                && !existing.IsCanceled)
            {
                return existing;
            }

            Task<IReadOnlyList<ImageMetadata>> task = Task.Run(() => ScanAsync(folderPath, order, mediaFilter, includeSubdirectories));
            _tasks[key] = task;
            return task;
        }
    }

    private Task<IReadOnlyList<ImageMetadata>> GetOrRepairCachedDimensionsAsync(
        string folderPath,
        bool includeSubdirectories,
        IReadOnlyList<ImageMetadata> cachedMedia)
    {
        string key = CreateKey(folderPath, PlaybackOrder.NameAsc, PlaybackMediaFilter.ImagesAndVideos, includeSubdirectories);
        lock (_sync)
        {
            if (_dimensionRepairTasks.TryGetValue(key, out Task<IReadOnlyList<ImageMetadata>>? existing)
                && !existing.IsFaulted && !existing.IsCanceled)
            {
                return existing;
            }

            Task<IReadOnlyList<ImageMetadata>> task = Task.Run(async () =>
            {
                IReadOnlyList<ImageMetadata> updated = await RefreshDimensionsAsync(cachedMedia, []).ConfigureAwait(false);
                _mediaCache.Save(folderPath, includeSubdirectories, updated);
                return updated;
            });
            _dimensionRepairTasks[key] = task;
            return task;
        }
    }

    private async Task<IReadOnlyList<ImageMetadata>> ScanAsync(string folderPath, PlaybackOrder order, PlaybackMediaFilter mediaFilter, bool includeSubdirectories)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return [];
        }

        if (!Directory.Exists(folderPath))
        {
            return _mediaCache.TryLoad(folderPath, includeSubdirectories, out IReadOnlyList<ImageMetadata> cachedMedia)
                ? ApplyOrderAndFilter(cachedMedia, order, mediaFilter)
                : [];
        }

        _mediaCache.TryLoad(folderPath, includeSubdirectories, out IReadOnlyList<ImageMetadata> previous, out bool hasCurrentDimensions);
        IReadOnlyList<ImageMetadata> scanned = ImageLibrary.ScanFolderMetadata(folderPath, PlaybackOrder.NameAsc, includeSubdirectories);
        scanned = await RefreshDimensionsAsync(scanned, hasCurrentDimensions ? previous : []).ConfigureAwait(false);
        _mediaCache.Save(folderPath, includeSubdirectories, scanned);
        return ApplyOrderAndFilter(scanned, order, mediaFilter);
    }

    private static async Task<IReadOnlyList<ImageMetadata>> RefreshDimensionsAsync(
        IReadOnlyList<ImageMetadata> media,
        IReadOnlyList<ImageMetadata> cachedMedia)
    {
        var cached = cachedMedia.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        var updated = new List<ImageMetadata>(media.Count);
        foreach (ImageMetadata item in media)
        {
            if (item.Kind != MediaKind.Image || string.Equals(Path.GetExtension(item.Path), ".ndf", StringComparison.OrdinalIgnoreCase))
            {
                updated.Add(item);
                continue;
            }

            if (cached.TryGetValue(item.Path, out ImageMetadata? previous)
                && previous.Length == item.Length
                && previous.ModifiedUtc == item.ModifiedUtc
                && previous.Width > 0 && previous.Height > 0)
            {
                updated.Add(item with { Width = previous.Width, Height = previous.Height });
                continue;
            }

            (int width, int height) = await ImageDimensionReader.ReadAsync(item.Path).ConfigureAwait(false);
            updated.Add(width > 0 && height > 0 ? item with { Width = width, Height = height } : item);
        }

        return updated;
    }

    private IReadOnlyList<ImageMetadata> ApplyOrderAndFilter(IReadOnlyList<ImageMetadata> media, PlaybackOrder order, PlaybackMediaFilter mediaFilter)
    {
        IReadOnlyList<ImageMetadata> images = ImageLibrary.FilterByMediaFilter(media, mediaFilter);
        if (order != PlaybackOrder.Random)
        {
            return ImageLibrary.SortImages(images, order);
        }

        lock (_sync)
        {
            return ImageLibrary.SortImages(images, order, _random);
        }
    }

    private static string CreateKey(string folderPath, PlaybackOrder order, PlaybackMediaFilter mediaFilter, bool includeSubdirectories)
    {
        string normalizedFolder = string.IsNullOrWhiteSpace(folderPath)
            ? string.Empty
            : Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return $"{includeSubdirectories}|{mediaFilter}|{order}|{normalizedFolder}";
    }
}
