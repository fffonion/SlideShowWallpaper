using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class PlaybackQueueTests
{
    [Fact]
    public void Next_returns_items_in_order_and_loops()
    {
        var queue = new PlaybackQueue(
        [
            new ImagePlaybackItem(@"C:\A\a.jpg"),
            new ImagePlaybackItem(@"C:\A\b.jpg"),
        ],
        PlaybackOrder.SequentialLoop);

        Assert.Equal(@"C:\A\a.jpg", queue.Next()?.Path);
        Assert.Equal(@"C:\A\b.jpg", queue.Next()?.Path);
        Assert.Equal(@"C:\A\a.jpg", queue.Next()?.Path);
    }

    [Fact]
    public void Next_WithSingleLoop_ReturnsFirstItemRepeatedly()
    {
        var queue = new PlaybackQueue(
        [
            new ImagePlaybackItem(@"C:\A\a.jpg"),
            new ImagePlaybackItem(@"C:\A\b.jpg"),
        ],
        PlaybackOrder.SingleLoop);

        Assert.Equal(@"C:\A\a.jpg", queue.Next()?.Path);
        Assert.Equal(@"C:\A\a.jpg", queue.Next()?.Path);
        Assert.Equal(@"C:\A\a.jpg", queue.Next()?.Path);
        Assert.Equal(1, queue.CurrentIndex);
    }

    [Fact]
    public void StartAfter_WithSingleLoop_ReturnsSelectedItemRepeatedly()
    {
        var queue = new PlaybackQueue(
        [
            new ImagePlaybackItem(@"C:\A\a.jpg"),
            new ImagePlaybackItem(@"C:\A\b.jpg"),
            new ImagePlaybackItem(@"C:\A\c.jpg"),
        ],
        PlaybackOrder.SingleLoop);

        queue.StartAfter(@"C:\A\b.jpg");

        Assert.Equal(@"C:\A\b.jpg", queue.Next()?.Path);
        Assert.Equal(@"C:\A\b.jpg", queue.Next()?.Path);
        Assert.Equal(2, queue.CurrentIndex);
    }

    [Fact]
    public void Next_updates_current_index_as_one_based_position()
    {
        var queue = new PlaybackQueue(
        [
            new ImagePlaybackItem(@"C:\A\a.jpg"),
            new ImagePlaybackItem(@"C:\A\b.jpg"),
            new ImagePlaybackItem(@"C:\A\c.jpg"),
        ],
        PlaybackOrder.SequentialLoop);

        _ = queue.Next();
        Assert.Equal(1, queue.CurrentIndex);
        Assert.Equal(3, queue.Count);

        _ = queue.Next();
        Assert.Equal(2, queue.CurrentIndex);
    }

    [Fact]
    public void Next_returns_null_for_empty_queue()
    {
        var queue = new PlaybackQueue([], PlaybackOrder.SequentialLoop);

        Assert.Null(queue.Next());
    }

    [Fact]
    public void ReplaceItems_resets_position_to_first_item()
    {
        var queue = new PlaybackQueue([new ImagePlaybackItem(@"C:\A\old.jpg")], PlaybackOrder.SequentialLoop);
        _ = queue.Next();

        queue.ReplaceItems([new ImagePlaybackItem(@"C:\A\new.jpg")]);

        Assert.Equal(@"C:\A\new.jpg", queue.Next()?.Path);
    }

    [Fact]
    public void ReplaceItems_WithSameNextItem_PreservesNextPosition()
    {
        var queue = new PlaybackQueue(
        [
            new ImagePlaybackItem(@"C:\A\a.jpg"),
            new ImagePlaybackItem(@"C:\A\b.jpg"),
            new ImagePlaybackItem(@"C:\A\c.jpg"),
        ],
        PlaybackOrder.SequentialLoop);
        _ = queue.Next();
        _ = queue.Next();

        queue.ReplaceItems(
        [
            new ImagePlaybackItem(@"C:\A\a.jpg"),
            new ImagePlaybackItem(@"C:\A\b.jpg"),
            new ImagePlaybackItem(@"C:\A\c.jpg"),
        ]);

        Assert.Equal(@"C:\A\c.jpg", queue.Next()?.Path);
    }

    [Fact]
    public void ReplaceItemsAfterCurrent_WithCurrentItemInNewList_StartsAfterCurrentItem()
    {
        var queue = new PlaybackQueue(
        [
            new ImagePlaybackItem(@"C:\A\a.jpg"),
            new ImagePlaybackItem(@"C:\A\b.jpg"),
        ],
        PlaybackOrder.SequentialLoop);
        _ = queue.Next();

        queue.ReplaceItemsAfterCurrent(
        [
            new ImagePlaybackItem(@"C:\A\a.jpg"),
            new ImagePlaybackItem(@"C:\A\new.jpg"),
        ]);

        Assert.Equal(@"C:\A\new.jpg", queue.Next()?.Path);
    }

    [Fact]
    public void Shuffle_WithRandomOrder_ReordersItems()
    {
        var queue = new PlaybackQueue(
        [
            new ImagePlaybackItem(@"C:\A\a.jpg"),
            new ImagePlaybackItem(@"C:\A\b.jpg"),
            new ImagePlaybackItem(@"C:\A\c.jpg"),
            new ImagePlaybackItem(@"C:\A\d.jpg"),
        ],
        PlaybackOrder.Random,
        new ZeroRandom());

        queue.Shuffle();

        Assert.Equal(@"C:\A\c.jpg", queue.Next()?.Path);
    }

    [Fact]
    public void Constructor_WithRandomOrder_ShufflesBeforeFirstNext()
    {
        var queue = new PlaybackQueue(
        [
            new ImagePlaybackItem(@"C:\A\a.jpg"),
            new ImagePlaybackItem(@"C:\A\b.jpg"),
            new ImagePlaybackItem(@"C:\A\c.jpg"),
            new ImagePlaybackItem(@"C:\A\d.jpg"),
        ],
        PlaybackOrder.Random,
        new ZeroRandom());

        Assert.Equal(@"C:\A\b.jpg", queue.Next()?.Path);
    }

    [Fact]
    public void FromOrderedItems_WithRandomOrder_UsesProvidedOrderBeforeLaterShuffle()
    {
        var queue = PlaybackQueue.FromOrderedItems(
        [
            new ImagePlaybackItem(@"C:\A\b.jpg"),
            new ImagePlaybackItem(@"C:\A\c.jpg"),
            new ImagePlaybackItem(@"C:\A\d.jpg"),
            new ImagePlaybackItem(@"C:\A\a.jpg"),
        ],
        PlaybackOrder.Random,
        new ZeroRandom());

        Assert.Equal(@"C:\A\b.jpg", queue.Next()?.Path);
        Assert.Equal(@"C:\A\c.jpg", queue.Next()?.Path);
        Assert.Equal(@"C:\A\d.jpg", queue.Next()?.Path);
        Assert.Equal(@"C:\A\a.jpg", queue.Next()?.Path);
        Assert.Equal(@"C:\A\c.jpg", queue.Next()?.Path);
    }

    private sealed class ZeroRandom : Random
    {
        public override int Next(int maxValue)
        {
            return 0;
        }
    }
}
