using Microsoft.UI.Dispatching;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Windows;

namespace SlideShowWallpaper.Services;

public sealed partial class WallpaperPlaybackCoordinator
{
    private void HideWindow(string monitorId)
    {
        _queueVersions[monitorId] = _queueVersions.TryGetValue(monitorId, out int currentVersion) ? currentVersion + 1 : 1;
        if (_timers.TryGetValue(monitorId, out DispatcherQueueTimer? timer))
        {
            timer.Stop();
        }

        if (_windows.TryGetValue(monitorId, out WallpaperWindow? window))
        {
            window.HideWallpaperWindow();
        }

        ConfigureVideoCoverageTimer();
    }

    private static void CloseWindowSafely(WallpaperWindow window)
    {
        try
        {
            window.Close();
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private void EnsureWindow(MonitorProfile profile, bool applyProfile = true)
    {
        if (!_windows.TryGetValue(profile.Id, out WallpaperWindow? window))
        {
            window = new WallpaperWindow(profile);
            string monitorId = profile.Id;
            window.VideoEnded += (_, _) => _ = ShowNextAsync(monitorId);
            _windows[profile.Id] = window;
            window.Activate();
        }
        else
        {
            window.ShowWallpaperWindow();
        }

        if (applyProfile)
        {
            window.ApplyProfile(profile);
        }

        window.SetForceMuted(_globalMute);
        _desktopHostService.HostOnDesktop(window, profile.Id, _monitorRects);
        ConfigureVideoCoverageTimer();
    }

    private async Task<bool> TryShowSelectedImageAsync(MonitorProfile profile)
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
        await ShowWindowMediaSafeAsync(profile.Id, _windows[profile.Id], CreatePlaybackItem(profile.SelectedImagePath));
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
            && _windows.Values.Any(window => window.IsShowingWallpaper)
            && _profiles.Values.Any(profile => profile.PauseVideoWhenOtherAppMaximized && !profile.IsStopped);
        if (shouldRun)
        {
            _videoCoverageTimer.Start();
            return;
        }

        _videoCoverageTimer.Stop();
        ApplyVideoCoverageState();
    }

    private void ApplyVideoCoverageState()
    {
        if (!_playbackEnabled || !_windows.Values.Any(window => window.IsShowingWallpaper))
        {
            return;
        }

        ForegroundWindowInfo? foregroundWindow = _foregroundWindowService.GetForegroundWindowInfo();
        foreach ((string monitorId, WallpaperWindow window) in _windows)
        {
            bool shouldPause = _profiles.TryGetValue(monitorId, out MonitorProfile? profile)
                && window.IsShowingWallpaper
                && !profile.IsStopped
                && _monitorRects.TryGetValue(monitorId, out Interop.NativeMethods.RECT monitorRect)
                && WindowCoveragePolicy.ShouldPauseVideo(
                    profile.PauseVideoWhenOtherAppMaximized ? foregroundWindow : null,
                    monitorRect,
                    Environment.ProcessId,
                    _pauseVideoWhenDisplayOffOrSleeping,
                    _isDisplayOffOrSleeping);
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
