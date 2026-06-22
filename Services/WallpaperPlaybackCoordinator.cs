using Microsoft.UI.Dispatching;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Windows;

namespace SlideShowWallpaper.Services;

public sealed class WallpaperPlaybackCoordinator
{
    private readonly MonitorService _monitorService;
    private readonly DesktopHostService _desktopHostService;
    private readonly ImageOrderService _imageOrderService;
    private readonly FolderChangeWatcherService _folderChangeWatcherService;
    private readonly ForegroundWindowService _foregroundWindowService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _videoCoverageTimer;
    private readonly Dictionary<string, WallpaperWindow> _windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DispatcherQueueTimer> _timers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlaybackQueue> _queues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _queueVersions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MonitorProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly MonitorProfileChangeTracker _profileChanges = new();
    private IReadOnlyDictionary<string, Interop.NativeMethods.RECT> _monitorRects = new Dictionary<string, Interop.NativeMethods.RECT>();
    private bool _playbackEnabled = true;

    public WallpaperPlaybackCoordinator(
        MonitorService monitorService,
        DesktopHostService desktopHostService,
        ImageOrderService imageOrderService,
        FolderChangeWatcherService folderChangeWatcherService,
        ForegroundWindowService? foregroundWindowService = null)
    {
        _monitorService = monitorService;
        _desktopHostService = desktopHostService;
        _imageOrderService = imageOrderService;
        _folderChangeWatcherService = folderChangeWatcherService;
        _foregroundWindowService = foregroundWindowService ?? new ForegroundWindowService();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _videoCoverageTimer = _dispatcherQueue.CreateTimer();
        _videoCoverageTimer.Interval = TimeSpan.FromSeconds(1);
        _videoCoverageTimer.IsRepeating = true;
        _videoCoverageTimer.Tick += (_, _) => ApplyVideoCoverageState();
    }

    public IReadOnlyList<MonitorProfile> CurrentMonitors => _monitorService.GetCurrentMonitors();

    public bool PlaybackEnabled => _playbackEnabled;

    public event EventHandler<OrderedImagesChangedEventArgs>? OrderedImagesChanged;

    public event EventHandler<CurrentWallpaperChangedEventArgs>? CurrentWallpaperChanged;

    public void ApplyProfiles(IReadOnlyList<MonitorProfile> profiles, bool playbackEnabled)
    {
        _playbackEnabled = playbackEnabled;
        if (!_playbackEnabled)
        {
            StopPlayback();
            return;
        }

        _monitorRects = _monitorService.GetMonitorRects();
        HashSet<string> activeProfileIds = profiles.Select(profile => profile.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string monitorId in _profiles.Keys.Where(id => !activeProfileIds.Contains(id)).ToArray())
        {
            _profiles.Remove(monitorId);
            _folderChangeWatcherService.Unwatch(monitorId);
        }

        foreach (MonitorProfile profile in profiles)
        {
            _profiles[profile.Id] = profile;
            ConfigureFolderWatcher(profile);
            if (profile.IsStopped)
            {
                CloseWindow(profile.Id);
                continue;
            }

            MonitorProfileChange change = _profileChanges.Update(profile);
            if (!change.HasChanges
                && _queues.TryGetValue(profile.Id, out PlaybackQueue? existingQueue)
                && existingQueue.Count > 0
                && _windows.ContainsKey(profile.Id))
            {
                continue;
            }

            if (change.QueueChanged)
            {
                StartRebuildQueue(profile);
                continue;
            }

            if (!_queues.TryGetValue(profile.Id, out PlaybackQueue? queue) || queue.Count == 0)
            {
                CloseWindow(profile.Id);
                continue;
            }

            if (!_windows.TryGetValue(profile.Id, out WallpaperWindow? window))
            {
                EnsureWindow(profile);
            }
            else
            {
                _desktopHostService.HostOnDesktop(window, profile.Id, _monitorRects);
                if (change.VisualChanged)
                {
                    _ = window.UpdateProfileWithTransitionAsync(profile);
                }
                else
                {
                    window.ApplyProfile(profile);
                }
            }

            ConfigureTimer(profile);
        }

        ConfigureVideoCoverageTimer();
        ApplyVideoCoverageState();
    }

    public void PauseOrResume(string monitorId, bool isPaused)
    {
        if (_timers.TryGetValue(monitorId, out DispatcherQueueTimer? timer))
        {
            if (isPaused)
            {
                timer.Stop();
            }
            else
            {
                timer.Start();
            }
        }
    }

    public async Task ShowNextAsync(string monitorId)
    {
        try
        {
            if (!_playbackEnabled)
            {
                return;
            }

            if (!_queues.TryGetValue(monitorId, out PlaybackQueue? queue) || !_windows.TryGetValue(monitorId, out WallpaperWindow? window))
            {
                return;
            }

            ImagePlaybackItem? item = queue.Next();
            if (item is null)
            {
                return;
            }

            await ShowWindowMediaAsync(monitorId, window, item);
            RestartTimer(monitorId);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    public void Shuffle(string monitorId)
    {
        if (_queues.TryGetValue(monitorId, out PlaybackQueue? queue))
        {
            queue.Shuffle();
        }
    }

    public async Task ShowImageAsync(MonitorProfile profile, string path, IReadOnlyList<string>? orderedPaths = null)
    {
        if (!_playbackEnabled || profile.IsStopped || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        _monitorRects = _monitorService.GetMonitorRects();
        try
        {
            IReadOnlyList<string> paths = orderedPaths is { Count: > 0 }
                ? orderedPaths
                : (await _imageOrderService.GetOrLoadOrderedImagesAsync(profile.FolderPath, profile.PlaybackOrder, profile.MediaFilter, CancellationToken.None))
                    .Select(image => image.Path)
                    .ToArray();
            ReplaceQueue(profile, paths, preserveInitialOrder: true);
            EnsureWindow(profile);
            ConfigureTimer(profile);
            if (_queues.TryGetValue(profile.Id, out PlaybackQueue? queue))
            {
                queue.StartAfter(path);
            }

            if (_windows.TryGetValue(profile.Id, out WallpaperWindow? window))
            {
                await ShowWindowMediaAsync(profile.Id, window, CreatePlaybackItem(path));
            }
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    public void Shutdown()
    {
        StopPlayback();
    }

    public void StopPlayback()
    {
        foreach (DispatcherQueueTimer timer in _timers.Values)
        {
            timer.Stop();
        }

        foreach (WallpaperWindow window in _windows.Values)
        {
            window.Close();
        }

        _windows.Clear();
        _timers.Clear();
        _queues.Clear();
        _queueVersions.Clear();
        _profiles.Clear();
        _profileChanges.Clear();
        _folderChangeWatcherService.Clear();
        _videoCoverageTimer.Stop();
    }

    private void CloseWindow(string monitorId)
    {
        _queueVersions[monitorId] = _queueVersions.TryGetValue(monitorId, out int currentVersion) ? currentVersion + 1 : 1;
        if (_timers.Remove(monitorId, out DispatcherQueueTimer? timer))
        {
            timer.Stop();
        }

        if (_windows.Remove(monitorId, out WallpaperWindow? window))
        {
            window.Close();
        }

        ConfigureVideoCoverageTimer();
        _profileChanges.Forget(monitorId);
    }

    private void ConfigureFolderWatcher(MonitorProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FolderPath))
        {
            _folderChangeWatcherService.Unwatch(profile.Id);
            return;
        }

        _folderChangeWatcherService.Watch(profile.Id, profile.FolderPath, () => DispatchFolderChange(profile.Id));
    }

    private void EnsureWindow(MonitorProfile profile)
    {
        if (!_windows.TryGetValue(profile.Id, out WallpaperWindow? window))
        {
            window = new WallpaperWindow(profile);
            string monitorId = profile.Id;
            window.VideoEnded += (_, _) => _ = ShowNextAsync(monitorId);
            _windows[profile.Id] = window;
            window.Activate();
        }

        window.ApplyProfile(profile);
        _desktopHostService.HostOnDesktop(window, profile.Id, _monitorRects);
        ConfigureVideoCoverageTimer();
    }

    private async void StartRebuildQueue(MonitorProfile profile)
    {
        int version = _queueVersions.TryGetValue(profile.Id, out int currentVersion) ? currentVersion + 1 : 1;
        _queueVersions[profile.Id] = version;
        bool selectedImageShown = TryShowSelectedImage(profile);
        try
        {
            IReadOnlyList<ImageMetadata> images = await _imageOrderService.GetOrLoadOrderedImagesAsync(profile.FolderPath, profile.PlaybackOrder, profile.MediaFilter, CancellationToken.None);
            if (!_playbackEnabled
                || profile.IsStopped
                || !_queueVersions.TryGetValue(profile.Id, out int latestVersion)
                || latestVersion != version)
            {
                return;
            }

            ReplaceQueue(profile, images.Select(image => image.Path), preserveInitialOrder: true);
            if (!_queues.TryGetValue(profile.Id, out PlaybackQueue? queue) || queue.Count == 0)
            {
                CloseWindow(profile.Id);
                return;
            }

            EnsureWindow(profile);
            ConfigureTimer(profile);
            if (selectedImageShown)
            {
                queue.StartAfter(profile.SelectedImagePath);
                PublishCurrentWallpaperChanged(profile.Id, profile.SelectedImagePath);
            }
            else
            {
                await ShowNextAsync(profile.Id);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private void DispatchFolderChange(string monitorId)
    {
        _dispatcherQueue.TryEnqueue(() => ReloadChangedFolderAsync(monitorId));
    }

    private async void ReloadChangedFolderAsync(string monitorId)
    {
        if (!_profiles.TryGetValue(monitorId, out MonitorProfile? profile) || string.IsNullOrWhiteSpace(profile.FolderPath))
        {
            return;
        }

        try
        {
            IReadOnlyList<ImageMetadata> images = await _imageOrderService.ReloadOrderedImagesAsync(profile.FolderPath, profile.PlaybackOrder, profile.MediaFilter, CancellationToken.None);
            OrderedImagesChanged?.Invoke(this, new OrderedImagesChangedEventArgs(profile.Id, images));
            if (!_playbackEnabled || profile.IsStopped)
            {
                return;
            }

            bool hadWindow = _windows.ContainsKey(profile.Id);
            ReplaceQueueAfterCurrent(profile, images.Select(image => image.Path));
            if (!_queues.TryGetValue(profile.Id, out PlaybackQueue? queue) || queue.Count == 0)
            {
                CloseWindow(profile.Id);
                return;
            }

            if (hadWindow)
            {
                if (!_timers.ContainsKey(profile.Id))
                {
                    ConfigureTimer(profile);
                }

                return;
            }

            EnsureWindow(profile);
            ConfigureTimer(profile);
            await ShowNextAsync(profile.Id);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private bool TryShowSelectedImage(MonitorProfile profile)
    {
        if (_windows.ContainsKey(profile.Id)
            || string.IsNullOrWhiteSpace(profile.FolderPath)
            || string.IsNullOrWhiteSpace(profile.SelectedImagePath)
            || !File.Exists(profile.SelectedImagePath)
            || !IsAllowedByMediaFilter(profile.SelectedImagePath, profile.MediaFilter))
        {
            return false;
        }

        EnsureWindow(profile);
        _ = ShowWindowMediaSafeAsync(profile.Id, _windows[profile.Id], CreatePlaybackItem(profile.SelectedImagePath));
        return true;
    }

    private async Task ShowWindowMediaSafeAsync(string monitorId, WallpaperWindow window, ImagePlaybackItem item)
    {
        try
        {
            await ShowWindowMediaAsync(monitorId, window, item);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private async Task ShowWindowMediaAsync(string monitorId, WallpaperWindow window, ImagePlaybackItem item)
    {
        if (!File.Exists(item.Path))
        {
            return;
        }

        if (!CanUpdateWindow(monitorId, window))
        {
            return;
        }

        if (item.Kind == MediaKind.Video)
        {
            bool loop = _profiles.TryGetValue(monitorId, out MonitorProfile? profile) && VideoPlaybackPolicy.ShouldLoopVideo(profile);
            string playbackPath = await NdfMediaService.MaterializeForPlaybackAsync(item.Path, CancellationToken.None);
            if (!CanUpdateWindow(monitorId, window))
            {
                return;
            }

            await window.ShowVideoAsync(playbackPath, loop);
        }
        else
        {
            string playbackPath = await NdfMediaService.MaterializeForPlaybackAsync(item.Path, CancellationToken.None);
            if (!CanUpdateWindow(monitorId, window))
            {
                return;
            }

            await window.ShowImageAsync(playbackPath);
        }

        PublishCurrentWallpaperChanged(monitorId, item.Path);
        ApplyVideoCoverageState();
    }

    private bool CanUpdateWindow(string monitorId, WallpaperWindow window)
    {
        return _playbackEnabled
            && _profiles.TryGetValue(monitorId, out MonitorProfile? profile)
            && !profile.IsStopped
            && _windows.TryGetValue(monitorId, out WallpaperWindow? currentWindow)
            && ReferenceEquals(currentWindow, window);
    }

    private void ReplaceQueue(MonitorProfile profile, IEnumerable<string> paths, bool preserveInitialOrder = false)
    {
        var items = paths.Select(CreatePlaybackItem);
        _queues[profile.Id] = preserveInitialOrder
            ? PlaybackQueue.FromOrderedItems(items, profile.PlaybackOrder)
            : new PlaybackQueue(items, profile.PlaybackOrder);
    }

    private void ReplaceQueueAfterCurrent(MonitorProfile profile, IEnumerable<string> paths)
    {
        ImagePlaybackItem[] items = paths.Select(CreatePlaybackItem).ToArray();
        if (_queues.TryGetValue(profile.Id, out PlaybackQueue? queue))
        {
            queue.ReplaceItemsAfterCurrent(items);
            return;
        }

        _queues[profile.Id] = PlaybackQueue.FromOrderedItems(items, profile.PlaybackOrder);
    }

    private static ImagePlaybackItem CreatePlaybackItem(string path)
    {
        return new ImagePlaybackItem(path, ImageLibrary.IsSupportedVideoPath(path) ? MediaKind.Video : MediaKind.Image);
    }

    private static bool IsAllowedByMediaFilter(string path, PlaybackMediaFilter mediaFilter)
    {
        return mediaFilter switch
        {
            PlaybackMediaFilter.ImagesOnly => ImageLibrary.IsSupportedImagePath(path),
            PlaybackMediaFilter.VideosOnly => ImageLibrary.IsSupportedVideoPath(path),
            _ => ImageLibrary.IsSupportedMediaPath(path),
        };
    }

    private void PublishCurrentWallpaperChanged(string monitorId, string path)
    {
        int currentIndex = _queues.TryGetValue(monitorId, out PlaybackQueue? queue) ? queue.CurrentIndex : 0;
        int totalCount = queue?.Count ?? 0;
        CurrentWallpaperChanged?.Invoke(this, new CurrentWallpaperChangedEventArgs(monitorId, path, currentIndex, totalCount));
    }

    private void ConfigureTimer(MonitorProfile profile)
    {
        if (!_timers.TryGetValue(profile.Id, out DispatcherQueueTimer? timer))
        {
            timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
            timer.Tick += async (_, _) => await ShowNextAsync(profile.Id);
            _timers[profile.Id] = timer;
        }

        timer.Stop();
        if (profile.PlaybackOrder == PlaybackOrder.SingleLoop)
        {
            return;
        }

        timer.Interval = TimeSpan.FromSeconds(Math.Max(5, profile.IntervalSeconds));
        if (!profile.IsPaused && !profile.IsStopped)
        {
            timer.Start();
        }
    }

    private void ConfigureVideoCoverageTimer()
    {
        bool shouldRun = _playbackEnabled
            && _windows.Count > 0
            && _profiles.Values.Any(profile => profile.PauseVideoWhenOtherAppMaximized && !profile.IsStopped);
        if (shouldRun)
        {
            _videoCoverageTimer.Start();
            return;
        }

        _videoCoverageTimer.Stop();
        foreach (WallpaperWindow window in _windows.Values)
        {
            window.SetVideoPausedByCoverage(false);
        }
    }

    private void ApplyVideoCoverageState()
    {
        if (!_playbackEnabled || _windows.Count == 0)
        {
            return;
        }

        ForegroundWindowInfo? foregroundWindow = _foregroundWindowService.GetForegroundWindowInfo();
        foreach ((string monitorId, WallpaperWindow window) in _windows)
        {
            bool shouldPause = _profiles.TryGetValue(monitorId, out MonitorProfile? profile)
                && profile.PauseVideoWhenOtherAppMaximized
                && !profile.IsStopped
                && _monitorRects.TryGetValue(monitorId, out Interop.NativeMethods.RECT monitorRect)
                && WindowCoveragePolicy.ShouldPauseVideo(foregroundWindow, monitorRect, Environment.ProcessId);
            window.SetVideoPausedByCoverage(shouldPause);
        }
    }

    private void RestartTimer(string monitorId)
    {
        if (!_profiles.TryGetValue(monitorId, out MonitorProfile? profile)
            || profile.IsPaused
            || profile.IsStopped
            || profile.PlaybackOrder == PlaybackOrder.SingleLoop
            || !_timers.TryGetValue(monitorId, out DispatcherQueueTimer? timer))
        {
            return;
        }

        timer.Stop();
        timer.Start();
    }
}

public sealed record OrderedImagesChangedEventArgs(string MonitorId, IReadOnlyList<ImageMetadata> Images);

public sealed record CurrentWallpaperChangedEventArgs(string MonitorId, string ImagePath, int CurrentIndex, int TotalCount);
