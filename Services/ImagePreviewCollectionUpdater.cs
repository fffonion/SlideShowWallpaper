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
        var existing = reusableItems.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        ImagePreviewItem[] targetItems = images
            .Select(image => existing.TryGetValue(image.Path, out ImagePreviewItem? item) ? item : new ImagePreviewItem(image))
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
}
