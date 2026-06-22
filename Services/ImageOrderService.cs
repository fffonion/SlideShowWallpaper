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
        Task<IReadOnlyList<ImageMetadata>> task = GetOrCreateTask(folderPath, order, reload: false);
        return await task.WaitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImageMetadata>> ReloadOrderedImagesAsync(string folderPath, PlaybackOrder order, CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<ImageMetadata>> task = GetOrCreateTask(folderPath, order, reload: true);
        return await task.WaitAsync(cancellationToken);
    }

    private Task<IReadOnlyList<ImageMetadata>> GetOrCreateTask(string folderPath, PlaybackOrder order, bool reload)
    {
        string key = CreateKey(folderPath, order);
        lock (_sync)
        {
            if (!reload
                && _tasks.TryGetValue(key, out Task<IReadOnlyList<ImageMetadata>>? existing)
                && !existing.IsFaulted
                && !existing.IsCanceled)
            {
                return existing;
            }

            Task<IReadOnlyList<ImageMetadata>> task = Task.Run(() => Scan(folderPath, order));
            _tasks[key] = task;
            return task;
        }
    }

    private IReadOnlyList<ImageMetadata> Scan(string folderPath, PlaybackOrder order)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        var images = new List<ImageMetadata>();
        foreach (string path in Directory.EnumerateFiles(folderPath))
        {
            if (!ImageLibrary.IsSupportedImagePath(path))
            {
                continue;
            }

            var info = new FileInfo(path);
            images.Add(new ImageMetadata(info.FullName, info.Name, info.LastWriteTimeUtc, info.Length));
        }

        lock (_sync)
        {
            return ImageLibrary.SortImages(images, order, _random);
        }
    }

    private static string CreateKey(string folderPath, PlaybackOrder order)
    {
        string normalizedFolder = string.IsNullOrWhiteSpace(folderPath)
            ? string.Empty
            : Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return $"{order}|{normalizedFolder}";
    }
}
