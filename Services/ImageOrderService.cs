using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class ImageOrderService
{
    private readonly object _sync = new();
    private readonly Random _random;
    private readonly Dictionary<string, Task<IReadOnlyList<ImageMetadata>>> _tasks = new(StringComparer.OrdinalIgnoreCase);

    public ImageOrderService()
        : this(Random.Shared)
    {
    }

    public ImageOrderService(Random random)
    {
        _random = random;
    }

    public async Task<IReadOnlyList<ImageMetadata>> GetOrLoadOrderedImagesAsync(string folderPath, PlaybackOrder order, CancellationToken cancellationToken)
    {
        return await GetOrLoadOrderedImagesAsync(folderPath, order, PlaybackMediaFilter.ImagesAndVideos, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageMetadata>> GetOrLoadOrderedImagesAsync(string folderPath, PlaybackOrder order, PlaybackMediaFilter mediaFilter, CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<ImageMetadata>> task = GetOrCreateTask(folderPath, order, mediaFilter, reload: false);
        return await task.WaitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImageMetadata>> ReloadOrderedImagesAsync(string folderPath, PlaybackOrder order, CancellationToken cancellationToken)
    {
        return await ReloadOrderedImagesAsync(folderPath, order, PlaybackMediaFilter.ImagesAndVideos, cancellationToken);
    }

    public async Task<IReadOnlyList<ImageMetadata>> ReloadOrderedImagesAsync(string folderPath, PlaybackOrder order, PlaybackMediaFilter mediaFilter, CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<ImageMetadata>> task = GetOrCreateTask(folderPath, order, mediaFilter, reload: true);
        return await task.WaitAsync(cancellationToken);
    }

    public void ClearCache()
    {
        lock (_sync)
        {
            _tasks.Clear();
        }
    }

    private Task<IReadOnlyList<ImageMetadata>> GetOrCreateTask(string folderPath, PlaybackOrder order, PlaybackMediaFilter mediaFilter, bool reload)
    {
        string key = CreateKey(folderPath, order, mediaFilter);
        lock (_sync)
        {
            if (!reload
                && _tasks.TryGetValue(key, out Task<IReadOnlyList<ImageMetadata>>? existing)
                && !existing.IsFaulted
                && !existing.IsCanceled)
            {
                return existing;
            }

            Task<IReadOnlyList<ImageMetadata>> task = Task.Run(() => Scan(folderPath, order, mediaFilter));
            _tasks[key] = task;
            return task;
        }
    }

    private IReadOnlyList<ImageMetadata> Scan(string folderPath, PlaybackOrder order, PlaybackMediaFilter mediaFilter)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        IReadOnlyList<ImageMetadata> images = ImageLibrary.FilterByMediaFilter(ImageLibrary.ScanFolderMetadata(folderPath, PlaybackOrder.NameAsc), mediaFilter);
        if (order != PlaybackOrder.Random)
        {
            return ImageLibrary.SortImages(images, order);
        }

        lock (_sync)
        {
            return ImageLibrary.SortImages(images, order, _random);
        }
    }

    private static string CreateKey(string folderPath, PlaybackOrder order, PlaybackMediaFilter mediaFilter)
    {
        string normalizedFolder = string.IsNullOrWhiteSpace(folderPath)
            ? string.Empty
            : Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return $"{mediaFilter}|{order}|{normalizedFolder}";
    }
}
