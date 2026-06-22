using System.Collections.Frozen;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class ImageLibrary
{
    private static readonly FrozenSet<string> SupportedExtensions = new[]
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".bmp",
        ".webp",
        ".heic",
        ".heif",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    public static IEnumerable<string> FilterSupportedImages(IEnumerable<string> paths)
    {
        return paths
            .Where(IsSupportedImagePath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ImageMetadata> SortImages(IEnumerable<ImageMetadata> images, PlaybackOrder order)
    {
        return SortImages(images, order, Random.Shared);
    }

    public static IReadOnlyList<ImageMetadata> SortImages(IEnumerable<ImageMetadata> images, PlaybackOrder order, Random random)
    {
        return order switch
        {
            PlaybackOrder.Random => Shuffle(images.OrderBy(image => image.FileName, StringComparer.OrdinalIgnoreCase), random),
            PlaybackOrder.NameDesc => images.OrderByDescending(image => image.FileName, StringComparer.OrdinalIgnoreCase).ToArray(),
            PlaybackOrder.ModifiedDateAsc => images.OrderBy(image => image.ModifiedUtc).ThenBy(image => image.FileName, StringComparer.OrdinalIgnoreCase).ToArray(),
            PlaybackOrder.ModifiedDateDesc => images.OrderByDescending(image => image.ModifiedUtc).ThenBy(image => image.FileName, StringComparer.OrdinalIgnoreCase).ToArray(),
            _ => images.OrderBy(image => image.FileName, StringComparer.OrdinalIgnoreCase).ToArray(),
        };
    }

    public static IReadOnlyList<ImageMetadata> ScanFolderMetadata(string folderPath, PlaybackOrder order)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        var images = Directory.EnumerateFiles(folderPath)
            .Where(IsSupportedImagePath)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new ImageMetadata(info.FullName, info.Name, info.LastWriteTimeUtc, info.Length);
            });

        return SortImages(images, order);
    }

    public static Task<IReadOnlyList<ImageMetadata>> ScanFolderMetadataAsync(string folderPath, PlaybackOrder order, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return (IReadOnlyList<ImageMetadata>)[];
            }

            var images = new List<ImageMetadata>();
            foreach (string path in Directory.EnumerateFiles(folderPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsSupportedImagePath(path))
                {
                    continue;
                }

                var info = new FileInfo(path);
                images.Add(new ImageMetadata(info.FullName, info.Name, info.LastWriteTimeUtc, info.Length));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return SortImages(images, order);
        }, cancellationToken);
    }

    public static IReadOnlyList<string> ScanFolder(string folderPath)
    {
        return ScanFolderMetadata(folderPath, PlaybackOrder.NameAsc)
            .Select(image => image.Path)
            .ToArray();
    }

    public static IReadOnlyList<string> ScanFolder(string folderPath, PlaybackOrder order)
    {
        return ScanFolderMetadata(folderPath, order)
            .Select(image => image.Path)
            .ToArray();
    }

    private static IReadOnlyList<ImageMetadata> Shuffle(IEnumerable<ImageMetadata> images, Random random)
    {
        ImageMetadata[] shuffled = images.ToArray();
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }
}
