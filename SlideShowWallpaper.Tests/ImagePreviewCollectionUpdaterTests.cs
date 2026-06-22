using System.Collections.ObjectModel;
using System.Collections.Specialized;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using SlideShowWallpaper.ViewModels;

namespace SlideShowWallpaper.Tests;

public sealed class ImagePreviewCollectionUpdaterTests
{
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

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

    [Fact]
    public void Apply_WithSamePreviewObjects_ReordersWithoutResettingCollection()
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
        var actions = new List<NotifyCollectionChangedAction>();
        items.CollectionChanged += (_, args) => actions.Add(args.Action);

        ImagePreviewCollectionUpdater.Apply(items, [second, first]);

        Assert.Same(secondItem, items[0]);
        Assert.Same(firstItem, items[1]);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
    }

    [Fact]
    public void Clear_RemovesPreviewItems()
    {
        var first = new ImageMetadata(@"C:\Wallpapers\a.png", "a.png", DateTime.UnixEpoch, 1);
        var second = new ImageMetadata(@"C:\Wallpapers\b.png", "b.png", DateTime.UnixEpoch, 1);
        var items = new ObservableCollection<ImagePreviewItem>
        {
            new(first),
            new(second),
        };

        ImagePreviewCollectionUpdater.Clear(items);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Thumbnail_ForVideoItem_LoadsThumbnailImage()
    {
        string videoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        string thumbnailPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(videoPath, [1, 2, 3]);
        await File.WriteAllBytesAsync(thumbnailPath, Convert.FromBase64String(OnePixelPngBase64));
        var metadata = new ImageMetadata(videoPath, "clip.mp4", DateTime.UnixEpoch, 1, MediaKind.Video);
        string? loadedThumbnailPath = null;
        var item = new ImagePreviewItem(
            metadata,
            (_, _) => Task.FromResult(thumbnailPath),
            path =>
            {
                loadedThumbnailPath = path;
                return null;
            });
        try
        {
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ImagePreviewItem.Thumbnail))
                {
                    changed.TrySetResult();
                }
            };

            _ = item.Thumbnail;
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(thumbnailPath, loadedThumbnailPath);
            Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, item.ImageVisibility);
            Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, item.PlaceholderVisibility);
        }
        finally
        {
            File.Delete(videoPath);
            File.Delete(thumbnailPath);
        }
    }

    [Fact]
    public async Task Thumbnail_WhenImageDecodeFails_KeepsPlaceholderInsteadOfLoadingOriginalImage()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        var metadata = new ImageMetadata(path, "bad.png", DateTime.UnixEpoch, 1);
        var item = new ImagePreviewItem(
            metadata,
            (_, _) => throw new InvalidDataException("decode failed"));
        try
        {
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ImagePreviewItem.Thumbnail))
                {
                    changed.TrySetResult();
                }
            };

            _ = item.Thumbnail;
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Null(item.Thumbnail);
            Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, item.ImageVisibility);
            Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, item.PlaceholderVisibility);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
