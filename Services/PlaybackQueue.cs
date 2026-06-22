using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class PlaybackQueue
{
    private readonly Random _random;
    private readonly PlaybackOrder _order;
    private List<ImagePlaybackItem> _items;
    private int _nextIndex;
    private string? _currentPath;

    public PlaybackQueue(IEnumerable<ImagePlaybackItem> items, PlaybackOrder order)
        : this(items, order, Random.Shared, shuffleInitial: true)
    {
    }

    public PlaybackQueue(IEnumerable<ImagePlaybackItem> items, PlaybackOrder order, Random random)
        : this(items, order, random, shuffleInitial: true)
    {
    }

    private PlaybackQueue(IEnumerable<ImagePlaybackItem> items, PlaybackOrder order, Random random, bool shuffleInitial)
    {
        _random = random;
        _order = order;
        _items = items.ToList();
        if (shuffleInitial)
        {
            ShuffleIfNeeded();
        }
    }

    public int Count => _items.Count;

    public static PlaybackQueue FromOrderedItems(IEnumerable<ImagePlaybackItem> items, PlaybackOrder order, Random? random = null)
    {
        return new PlaybackQueue(items, order, random ?? Random.Shared, shuffleInitial: false);
    }

    public ImagePlaybackItem? Next()
    {
        if (_items.Count == 0)
        {
            return null;
        }

        ImagePlaybackItem item = _items[_nextIndex];
        _currentPath = item.Path;

        _nextIndex = (_nextIndex + 1) % _items.Count;
        if (_nextIndex == 0)
        {
            ShuffleIfNeeded();
        }

        return item;
    }

    public void ReplaceItems(IEnumerable<ImagePlaybackItem> items)
    {
        string? nextPath = _items.Count == 0 ? null : _items[_nextIndex].Path;
        _items = items.ToList();
        _nextIndex = string.IsNullOrWhiteSpace(nextPath)
            ? 0
            : Math.Max(0, _items.FindIndex(item => string.Equals(item.Path, nextPath, StringComparison.OrdinalIgnoreCase)));
    }

    public void ReplaceItemsAfterCurrent(IEnumerable<ImagePlaybackItem> items)
    {
        string? currentPath = _currentPath;
        _items = items.ToList();
        _nextIndex = 0;
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            StartAfter(currentPath);
        }
    }

    public void Shuffle()
    {
        if (_order != PlaybackOrder.Random)
        {
            return;
        }

        ShuffleItems();
        _nextIndex = 0;
    }

    public void StartAfter(string path)
    {
        int index = _items.FindIndex(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _currentPath = _items[index].Path;
            _nextIndex = (index + 1) % _items.Count;
        }
    }

    private void ShuffleIfNeeded()
    {
        if (_order != PlaybackOrder.Random)
        {
            return;
        }

        ShuffleItems();
    }

    private void ShuffleItems()
    {
        for (int i = _items.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (_items[i], _items[j]) = (_items[j], _items[i]);
        }
    }
}
