using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class PlaybackQueue
{
    private readonly Random _random;
    private readonly PlaybackOrder _order;
    private List<ImagePlaybackItem> _items;
    private int _nextIndex;

    public PlaybackQueue(IEnumerable<ImagePlaybackItem> items, PlaybackOrder order)
        : this(items, order, Random.Shared)
    {
    }

    public PlaybackQueue(IEnumerable<ImagePlaybackItem> items, PlaybackOrder order, Random random)
    {
        _random = random;
        _order = order;
        _items = items.ToList();
        ShuffleIfNeeded();
    }

    public int Count => _items.Count;

    public ImagePlaybackItem? Next()
    {
        if (_items.Count == 0)
        {
            return null;
        }

        ImagePlaybackItem item = _items[_nextIndex];

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
