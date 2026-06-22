namespace SlideShowWallpaper.Services;

public sealed class FolderChangeWatcherService : IDisposable
{
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromSeconds(10);
    private readonly Dictionary<string, WatchRegistration> _registrations = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _debounceDelay;
    private bool _disposed;

    public FolderChangeWatcherService()
        : this(DefaultDebounceDelay)
    {
    }

    public FolderChangeWatcherService(TimeSpan debounceDelay)
    {
        _debounceDelay = debounceDelay;
    }

    public void Watch(string key, string folderPath, Action changed)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FolderChangeWatcherService));
        }

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            Unwatch(key);
            return;
        }

        string normalizedFolder = NormalizeFolder(folderPath);
        if (_registrations.TryGetValue(key, out WatchRegistration? existing)
            && string.Equals(existing.FolderPath, normalizedFolder, StringComparison.OrdinalIgnoreCase))
        {
            existing.UpdateCallback(changed);
            return;
        }

        Unwatch(key);
        _registrations[key] = new WatchRegistration(normalizedFolder, changed, _debounceDelay);
    }

    public void Unwatch(string key)
    {
        if (_registrations.Remove(key, out WatchRegistration? registration))
        {
            registration.Dispose();
        }
    }

    public void Clear()
    {
        foreach (WatchRegistration registration in _registrations.Values)
        {
            registration.Dispose();
        }

        _registrations.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _disposed = true;
    }

    private static string NormalizeFolder(string folderPath)
    {
        return Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed class WatchRegistration : IDisposable
    {
        private readonly Lock _lock = new();
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _timer;
        private Action _changed;
        private bool _disposed;

        public WatchRegistration(string folderPath, Action changed, TimeSpan debounceDelay)
        {
            FolderPath = folderPath;
            _changed = changed;
            _timer = new Timer(_ => RaiseChanged(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _watcher = new FileSystemWatcher(folderPath)
            {
                Filter = "*.*",
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            };
            _watcher.Created += (_, _) => Schedule(debounceDelay);
            _watcher.Changed += (_, _) => Schedule(debounceDelay);
            _watcher.Deleted += (_, _) => Schedule(debounceDelay);
            _watcher.Renamed += (_, _) => Schedule(debounceDelay);
            _watcher.Error += (_, _) => Schedule(debounceDelay);
            _watcher.EnableRaisingEvents = true;
        }

        public string FolderPath { get; }

        public void UpdateCallback(Action changed)
        {
            lock (_lock)
            {
                _changed = changed;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _watcher.Dispose();
                _timer.Dispose();
            }
        }

        private void Schedule(TimeSpan debounceDelay)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _timer.Change(debounceDelay, Timeout.InfiniteTimeSpan);
            }
        }

        private void RaiseChanged()
        {
            Action changed;
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                changed = _changed;
            }

            changed();
        }
    }
}
