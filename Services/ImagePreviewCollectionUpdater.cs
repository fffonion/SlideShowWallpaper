using System.Collections.ObjectModel;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.ViewModels;

namespace SlideShowWallpaper.Services;

public static class ImagePreviewCollectionUpdater
{
    public static void Apply(ObservableCollection<ImagePreviewItem> items, IReadOnlyList<ImageMetadata> images)
    {
        var existing = items.ToDictionary(item => item.Path, StringComparer.OrdinalIgnoreCase);
        items.Clear();
        foreach (ImageMetadata image in images)
        {
            items.Add(existing.TryGetValue(image.Path, out ImagePreviewItem? item) ? item : new ImagePreviewItem(image));
        }
    }
}
