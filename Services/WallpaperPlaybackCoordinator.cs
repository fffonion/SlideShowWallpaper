using Microsoft.UI.Dispatching;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Windows;

namespace SlideShowWallpaper.Services;

public sealed partial class WallpaperPlaybackCoordinator
{
    private readonly MonitorService _monitorService;
    private readonly DesktopHostService _desktopHostService;
    private readonly ImageOrderService _imageOrderService;
    private readonly FolderChangeWatcherService _folderChangeWatcherService;
    private readonly HardwareMonitorService _hardwareMonitorService;
    private readonly ForegroundWindowService _foregroundWindowService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _videoCoverageTimer;
    private readonly DispatcherQueueTimer _hardwareOverlayTimer;
    private readonly Dictionary<string, WallpaperWindow> _windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DispatcherQueueTimer> _timers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PlaybackQueue> _queues = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _queueVersions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MonitorProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly MonitorProfileChangeTracker _profileChanges = new();
    private IReadOnlyDictionary<string, Interop.NativeMethods.RECT> _monitorRects = new Dictionary<string, Interop.NativeMethods.RECT>();
    private bool _playbackEnabled = true;
    private bool _autoTrackNewFiles = true;
    private bool _globalMute = true;
    private bool _pauseVideoWhenDisplayOffOrSleeping = true;
    private bool _isDisplayOffOrSleeping;
    private bool _hardwareOverlayRefreshInProgress;
    private HardwareMonitorConfig _hardwareMonitorConfig = new();

    public WallpaperPlaybackCoordinator(
        MonitorService monitorService,
        DesktopHostService desktopHostService,
        ImageOrderService imageOrderService,
        FolderChangeWatcherService folderChangeWatcherService,
        HardwareMonitorService? hardwareMonitorService = null,
        ForegroundWindowService? foregroundWindowService = null)
    {
        _monitorService = monitorService;
        _desktopHostService = desktopHostService;
        _imageOrderService = imageOrderService;
        _folderChangeWatcherService = folderChangeWatcherService;
        _hardwareMonitorService = hardwareMonitorService ?? new HardwareMonitorService();
        _foregroundWindowService = foregroundWindowService ?? new ForegroundWindowService();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _videoCoverageTimer = _dispatcherQueue.CreateTimer();
        _videoCoverageTimer.Interval = TimeSpan.FromSeconds(1);
        _videoCoverageTimer.IsRepeating = true;
        _videoCoverageTimer.Tick += (_, _) => ApplyVideoCoverageState();
        _hardwareOverlayTimer = _dispatcherQueue.CreateTimer();
        _hardwareOverlayTimer.Interval = TimeSpan.FromSeconds(HardwareMonitorConfig.DefaultRefreshIntervalSeconds);
        _hardwareOverlayTimer.IsRepeating = true;
        _hardwareOverlayTimer.Tick += async (_, _) => await RefreshHardwareOverlayAsync();
    }

    public IReadOnlyList<MonitorProfile> CurrentMonitors => _monitorService.GetCurrentMonitors();

    public bool PlaybackEnabled => _playbackEnabled;

    public event EventHandler<OrderedImagesChangedEventArgs>? OrderedImagesChanged;

    public event EventHandler<CurrentWallpaperChangedEventArgs>? CurrentWallpaperChanged;

    public void ApplyProfiles(
        IReadOnlyList<MonitorProfile> profiles,
        bool playbackEnabled,
        bool autoTrackNewFiles = true,
        bool globalMute = true,
        bool pauseVideoWhenDisplayOffOrSleeping = true,
        HardwareMonitorConfig? hardwareMonitorConfig = null)
    {
        bool globalMuteChanged = _globalMute != globalMute;
        _playbackEnabled = playbackEnabled;
        _autoTrackNewFiles = autoTrackNewFiles;
        _globalMute = globalMute;
        _pauseVideoWhenDisplayOffOrSleeping = pauseVideoWhenDisplayOffOrSleeping;
        _hardwareMonitorConfig = hardwareMonitorConfig ?? new HardwareMonitorConfig();
        if (!_playbackEnabled)
        {
            StopPlayback();
            return;
        }

        if (!_autoTrackNewFiles)
        {
            _folderChangeWatcherService.Clear();
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
            if (_autoTrackNewFiles)
            {
                ConfigureFolderWatcher(profile);
            }

            if (profile.IsStopped)
            {
                HideWindow(profile.Id);
                _profileChanges.Forget(profile.Id);
                continue;
            }

            MonitorProfileChange change = _profileChanges.Update(profile);
            if (!change.HasChanges
                && _queues.TryGetValue(profile.Id, out PlaybackQueue? existingQueue)
                && existingQueue.Count > 0
                && _windows.TryGetValue(profile.Id, out WallpaperWindow? existingWindow)
                && existingWindow.IsShowingWallpaper)
            {
                if (globalMuteChanged)
                {
                    existingWindow.SetForceMuted(_globalMute);
                }

                continue;
            }

            if (change.QueueChanged)
            {
                StartRebuildQueue(profile);
                continue;
            }

            if (!_queues.TryGetValue(profile.Id, out PlaybackQueue? queue) || queue.Count == 0)
            {
                HideWindow(profile.Id);
                continue;
            }

            bool needsMedia = !_windows.TryGetValue(profile.Id, out WallpaperWindow? window) || !window.IsShowingWallpaper;
            if (window is null)
            {
                EnsureWindow(profile);
            }
            else
            {
                window.ShowWallpaperWindow();
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
            if (needsMedia)
            {
                _ = ShowNextAsync(profile.Id);
            }
        }

        ConfigureVideoCoverageTimer();
        ApplyVideoCoverageState();
        ConfigureHardwareOverlayTimer();
        _ = RefreshHardwareOverlayAsync();
    }

    public void SetDisplayPowerVideoPause(bool isPaused)
    {
        bool stateChanged = _isDisplayOffOrSleeping != isPaused;
        _isDisplayOffOrSleeping = isPaused;
        if (!isPaused)
        {
            RehostActiveWindows();
        }

        if (!stateChanged && isPaused)
        {
            return;
        }

        ApplyVideoCoverageState();
    }

    private void RehostActiveWindows()
    {
        _monitorRects = _monitorService.GetMonitorRects();
        foreach ((string monitorId, WallpaperWindow window) in _windows)
        {
            if (window.IsShowingWallpaper)
            {
                _desktopHostService.HostOnDesktop(window, monitorId, _monitorRects);
            }
        }
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
            EnsureWindow(profile, applyProfile: false);
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
            CloseWindowSafely(window);
        }

        _windows.Clear();
        _timers.Clear();
        _queues.Clear();
        _queueVersions.Clear();
        _profiles.Clear();
        _profileChanges.Clear();
        _folderChangeWatcherService.Clear();
        _videoCoverageTimer.Stop();
        _hardwareOverlayTimer.Stop();
    }

    private void ConfigureHardwareOverlayTimer()
    {
        bool shouldRun = _playbackEnabled
            && _hardwareMonitorConfig.IsEnabled
            && _windows.Values.Any(window => window.IsShowingWallpaper);
        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(1, _hardwareMonitorConfig.RefreshIntervalSeconds));
        if (_hardwareOverlayTimer.Interval != interval)
        {
            _hardwareOverlayTimer.Stop();
            _hardwareOverlayTimer.Interval = interval;
        }

        if (shouldRun)
        {
            _hardwareOverlayTimer.Start();
            return;
        }

        _hardwareOverlayTimer.Stop();
        ClearHardwareOverlay();
    }

    private async Task RefreshHardwareOverlayAsync()
    {
        if (!_playbackEnabled || !_hardwareMonitorConfig.IsEnabled)
        {
            ClearHardwareOverlay();
            return;
        }

        if (_hardwareOverlayRefreshInProgress)
        {
            return;
        }

        HardwareMonitorSnapshot snapshot;
        _hardwareOverlayRefreshInProgress = true;
        try
        {
            snapshot = await Task.Run(_hardwareMonitorService.GetSnapshot);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            ClearHardwareOverlay();
            return;
        }
        finally
        {
            _hardwareOverlayRefreshInProgress = false;
        }

        if (!_playbackEnabled || !_hardwareMonitorConfig.IsEnabled)
        {
            ClearHardwareOverlay();
            return;
        }

        string text = HardwareOverlayTextRenderer.Render(_hardwareMonitorConfig, snapshot);
        foreach ((string monitorId, WallpaperWindow window) in _windows)
        {
            bool isTarget = string.IsNullOrWhiteSpace(_hardwareMonitorConfig.TargetMonitorId)
                || string.Equals(_hardwareMonitorConfig.TargetMonitorId, monitorId, StringComparison.OrdinalIgnoreCase);
            var state = new HardwareOverlayState(
                _hardwareMonitorConfig.IsEnabled && isTarget && window.IsShowingWallpaper && !string.IsNullOrWhiteSpace(text),
                text,
                _hardwareMonitorConfig.X,
                _hardwareMonitorConfig.Y,
                _hardwareMonitorConfig.FontSize,
                _hardwareMonitorConfig.Opacity);
            window.SetHardwareOverlay(state);
        }
    }

    private void ClearHardwareOverlay()
    {
        foreach (WallpaperWindow window in _windows.Values)
        {
            window.SetHardwareOverlay(new HardwareOverlayState(false, string.Empty, 0, 0, 0, 0));
        }
    }

}

public sealed record OrderedImagesChangedEventArgs(string MonitorId, IReadOnlyList<ImageMetadata> Images);

public sealed record CurrentWallpaperChangedEventArgs(string MonitorId, string ImagePath, int CurrentIndex, int TotalCount);
