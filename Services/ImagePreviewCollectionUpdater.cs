using System.Collections.ObjectModel;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.ViewModels;

namespace SlideShowWallpaper.Services;

public static class ImagePreviewCollectionUpdater
{
    public static void Apply(ObservableCollection<ImagePreviewItem> items, IReadOnlyList<ImageMetadata> images)
    {
        Apply(items, images, items);
    }

    public static void Apply(ObservableCollection<ImagePreviewItem> items, IReadOnlyList<ImageMetadata> images, IEnumerable<ImagePreviewItem> reusableItems)
    {
        Apply(items, images, reusableItems, null);
    }

    public static void Apply(
        ObservableCollection<ImagePreviewItem> items,
        IReadOnlyList<ImageMetadata> images,
        IEnumerable<ImagePreviewItem> reusableItems,
        Func<ImageMetadata, CancellationToken, Task<string>>? thumbnailLoader)
    {
        Apply(items, images, reusableItems, thumbnailLoader, firstPath: null);
    }

    public static void ApplyWithFirstItem(
        ObservableCollection<ImagePreviewItem> items,
        IReadOnlyList<ImageMetadata> images,
        IEnumerable<ImagePreviewItem> reusableItems,
        Func<ImageMetadata, CancellationToken, Task<string>>? thumbnailLoader,
        string? firstPath)
    {
        Apply(items, images, reusableItems, thumbnailLoader, firstPath);
    }

    public static bool MoveToFront(ObservableCollection<ImagePreviewItem> items, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        int index = IndexOf(items, path);
        if (index <= 0)
        {
            return index == 0;
        }

        items.Move(index, 0);
        return true;
    }

    private static void Apply(
        ObservableCollection<ImagePreviewItem> items,
        IReadOnlyList<ImageMetadata> images,
        IEnumerable<ImagePreviewItem> reusableItems,
        Func<ImageMetadata, CancellationToken, Task<string>>? thumbnailLoader,
        string? firstPath)
    {
        var existing = reusableItems.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        ImagePreviewItem[] targetItems = MoveFirst(images, firstPath)
            .Select(image => existing.TryGetValue(image.Path, out ImagePreviewItem? item) ? item : CreatePreviewItem(image, thumbnailLoader))
            .ToArray();

        for (int index = items.Count - 1; index >= 0; index--)
        {
            if (!targetItems.Any(item => string.Equals(item.Path, items[index].Path, StringComparison.OrdinalIgnoreCase)))
            {
                items.RemoveAt(index);
            }
        }

        for (int targetIndex = 0; targetIndex < targetItems.Length; targetIndex++)
        {
            ImagePreviewItem targetItem = targetItems[targetIndex];
            int currentIndex = IndexOf(items, targetItem.Path);
            if (currentIndex < 0)
            {
                items.Insert(targetIndex, targetItem);
            }
            else if (currentIndex != targetIndex)
            {
                items.Move(currentIndex, targetIndex);
            }
        }
    }

    public static void Clear(ObservableCollection<ImagePreviewItem> items)
    {
        foreach (ImagePreviewItem item in items)
        {
            item.ClearThumbnail();
        }

        items.Clear();
    }

    private static int IndexOf(ObservableCollection<ImagePreviewItem> items, string path)
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (string.Equals(items[index].Path, path, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static ImagePreviewItem CreatePreviewItem(ImageMetadata image, Func<ImageMetadata, CancellationToken, Task<string>>? thumbnailLoader)
    {
        return thumbnailLoader is null ? new ImagePreviewItem(image) : new ImagePreviewItem(image, thumbnailLoader);
    }

    private static IEnumerable<ImageMetadata> MoveFirst(IReadOnlyList<ImageMetadata> images, string? firstPath)
    {
        if (string.IsNullOrWhiteSpace(firstPath))
        {
            return images;
        }

        ImageMetadata? first = images.FirstOrDefault(image => string.Equals(image.Path, firstPath, StringComparison.OrdinalIgnoreCase));
        return first is null
            ? images
            : images.Where(image => !string.Equals(image.Path, firstPath, StringComparison.OrdinalIgnoreCase)).Prepend(first);
    }
}
