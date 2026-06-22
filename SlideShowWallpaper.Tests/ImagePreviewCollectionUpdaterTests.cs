using System.Collections.ObjectModel;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using SlideShowWallpaper.ViewModels;

namespace SlideShowWallpaper.Tests;

public sealed class ImagePreviewCollectionUpdaterTests
{
    [Fact]
    public void Apply_ReordersItemsAndReusesExistingPreviewObjects()
    {
        var first = new ImageMetadata(@"C:\Wallpapers\a.png", "a.png", DateTime.UnixEpoch, 1);
        var second = new ImageMetadata(@"C:\Wallpapers\b.png", "b.png", DateTime.UnixEpoch, 1);
        var firstItem = new ImagePreviewItem(first);
        var secondItem = new ImagePreviewItem(second);
        var items = new ObservableCollection<ImagePreviewItem>
        {
            firstItem,
            secondItem,
        };

        ImagePreviewCollectionUpdater.Apply(items, [second, first]);

        Assert.Same(secondItem, items[0]);
        Assert.Same(firstItem, items[1]);
    }

    [Fact]
    public void Apply_WithReusableItems_ReusesPreviewObjectsAfterVisibleListWasCleared()
    {
        var first = new ImageMetadata(@"C:\Wallpapers\a.png", "a.png", DateTime.UnixEpoch, 1);
        var second = new ImageMetadata(@"C:\Wallpapers\b.png", "b.png", DateTime.UnixEpoch, 1);
        var firstItem = new ImagePreviewItem(first);
        var secondItem = new ImagePreviewItem(second);
        var items = new ObservableCollection<ImagePreviewItem>
        {
            firstItem,
            secondItem,
        };
        ImagePreviewItem[] reusableItems = [.. items];
        items.Clear();

        ImagePreviewCollectionUpdater.Apply(items, [second, first], reusableItems);

        Assert.Same(secondItem, items[0]);
        Assert.Same(firstItem, items[1]);
    }
}
