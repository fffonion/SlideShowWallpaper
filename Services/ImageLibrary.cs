using System.Collections.Frozen;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class ImageLibrary
{
    private const int MinimumNdfShortSide = 1080;

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

    private static readonly FrozenSet<string> SupportedVideoExtensions = new[]
    {
        ".mp4",
        ".m4v",
        ".mov",
        ".wmv",
        ".avi",
        ".mkv",
        ".webm",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedImagePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (NdfMediaService.TryGetMediaInfo(path, out NdfMediaInfo info))
        {
            return info.Kind == MediaKind.Image && IsLargeEnoughNdfMedia(info);
        }

        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    public static bool IsSupportedVideoPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (NdfMediaService.TryGetMediaInfo(path, out NdfMediaInfo info))
        {
            return info.Kind == MediaKind.Video && IsLargeEnoughNdfMedia(info);
        }

        return SupportedVideoExtensions.Contains(Path.GetExtension(path));
    }

    public static bool IsSupportedMediaPath(string? path)
    {
        return IsSupportedImagePath(path) || IsSupportedVideoPath(path);
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

    public static IReadOnlyList<ImageMetadata> FilterByMediaFilter(IEnumerable<ImageMetadata> media, PlaybackMediaFilter filter)
    {
        return filter switch
        {
            PlaybackMediaFilter.ImagesOnly => media.Where(item => item.Kind == MediaKind.Image).ToArray(),
            PlaybackMediaFilter.PortraitImagesOnly => media.Where(item => item.Kind == MediaKind.Image && item.Height > item.Width && item.Width > 0).ToArray(),
            PlaybackMediaFilter.LandscapeImagesOnly => media.Where(item => item.Kind == MediaKind.Image && item.Width > item.Height && item.Height > 0).ToArray(),
            PlaybackMediaFilter.VideosOnly => media.Where(item => item.Kind == MediaKind.Video).ToArray(),
            _ => media.ToArray(),
        };
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
        return ScanFolderMetadata(folderPath, order, includeSubdirectories: false);
    }

    public static IReadOnlyList<ImageMetadata> ScanFolderMetadata(string folderPath, PlaybackOrder order, bool includeSubdirectories)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return [];
        }

        var images = EnumerateMediaFiles(folderPath, includeSubdirectories)
            .Where(IsSupportedMediaPath)
            .Select(CreateMetadata);

        return SortImages(images, order);
    }

    public static Task<IReadOnlyList<ImageMetadata>> ScanFolderMetadataAsync(string folderPath, PlaybackOrder order, CancellationToken cancellationToken)
    {
        return ScanFolderMetadataAsync(folderPath, order, includeSubdirectories: false, cancellationToken);
    }

    public static Task<IReadOnlyList<ImageMetadata>> ScanFolderMetadataAsync(
        string folderPath,
        PlaybackOrder order,
        bool includeSubdirectories,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return (IReadOnlyList<ImageMetadata>)[];
            }

            var images = new List<ImageMetadata>();
            foreach (string path in EnumerateMediaFiles(folderPath, includeSubdirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsSupportedMediaPath(path))
                {
                    continue;
                }

                images.Add(CreateMetadata(path));
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

    private static IEnumerable<string> EnumerateMediaFiles(string folderPath, bool includeSubdirectories)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = includeSubdirectories,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };

        return Directory.EnumerateFiles(folderPath, "*", options);
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

    private static MediaKind GetMediaKind(string path)
    {
        if (NdfMediaService.TryGetMediaInfo(path, out NdfMediaInfo info))
        {
            return info.Kind;
        }

        return IsSupportedVideoPath(path) ? MediaKind.Video : MediaKind.Image;
    }

    private static ImageMetadata CreateMetadata(string path)
    {
        var linkInfo = new FileInfo(path);
        FileInfo metadataInfo = FileLinkResolver.GetFinalFileInfo(path);
        MediaKind kind = GetMediaKind(linkInfo.FullName);
        (int width, int height) = GetMediaDimensions(linkInfo.FullName, kind);
        return new ImageMetadata(linkInfo.FullName, linkInfo.Name, metadataInfo.LastWriteTimeUtc, metadataInfo.Length, kind, width, height);
    }

    private static (int Width, int Height) GetMediaDimensions(string path, MediaKind kind)
    {
        if (NdfMediaService.TryGetMediaInfo(path, out NdfMediaInfo info))
        {
            return (info.Width, info.Height);
        }

        if (kind == MediaKind.Image && ImageDimensionReader.TryRead(path, out int width, out int height))
        {
            return (width, height);
        }

        return (0, 0);
    }

    private static bool IsLargeEnoughNdfMedia(NdfMediaInfo info)
    {
        return info.Width <= 0 || info.Height <= 0 || Math.Min(info.Width, info.Height) >= MinimumNdfShortSide;
    }
}
