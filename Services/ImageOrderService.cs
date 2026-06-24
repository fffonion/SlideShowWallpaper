using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class ImageOrderService
{
    private readonly object _sync = new();
    private readonly Random _random;
    private readonly Dictionary<string, Task<IReadOnlyList<ImageMetadata>>> _tasks = new(StringComparer.OrdinalIgnoreCase);
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

        if (_mediaCache.TryLoad(folderPath, includeSubdirectories, out IReadOnlyList<ImageMetadata> cachedMedia))
        {
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

            Task<IReadOnlyList<ImageMetadata>> task = Task.Run(() => Scan(folderPath, order, mediaFilter, includeSubdirectories));
            _tasks[key] = task;
            return task;
        }
    }

    private IReadOnlyList<ImageMetadata> Scan(string folderPath, PlaybackOrder order, PlaybackMediaFilter mediaFilter, bool includeSubdirectories)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        IReadOnlyList<ImageMetadata> scanned = ImageLibrary.ScanFolderMetadata(folderPath, PlaybackOrder.NameAsc, includeSubdirectories);
        _mediaCache.Save(folderPath, includeSubdirectories, scanned);
        return ApplyOrderAndFilter(scanned, order, mediaFilter);
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
