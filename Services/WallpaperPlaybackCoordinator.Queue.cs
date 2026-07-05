using Microsoft.UI.Dispatching;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Windows;

namespace SlideShowWallpaper.Services;

public sealed partial class WallpaperPlaybackCoordinator
{
    private static readonly TimeSpan FolderAvailabilityRetryDelay = TimeSpan.FromSeconds(15);
    private readonly Dictionary<string, DispatcherQueueTimer> _folderAvailabilityRetryTimers = new(StringComparer.OrdinalIgnoreCase);

    private void ConfigureFolderWatcher(MonitorProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FolderPath))
        {
            _folderChangeWatcherService.Unwatch(profile.Id);
            return;
        }

        _folderChangeWatcherService.Watch(profile.Id, profile.FolderPath, profile.IncludeSubdirectories, () => DispatchFolderChange(profile.Id));
    }

    private async void StartRebuildQueue(MonitorProfile profile)
    {
        int version = _queueVersions.TryGetValue(profile.Id, out int currentVersion) ? currentVersion + 1 : 1;
        _queueVersions[profile.Id] = version;
        bool selectedImageShown = false;
        try
        {
            selectedImageShown = await TryShowSelectedImageAsync(profile);
            ImageOrderLoadResult result = await _imageOrderService.GetOrLoadOrderedImagesWithStatusAsync(
                profile.FolderPath,
                profile.PlaybackOrder,
                profile.MediaFilter,
                profile.IncludeSubdirectories,
                CancellationToken.None);
            IReadOnlyList<ImageMetadata> images = result.Images;
            if (images.Count == 0 && IsFolderTemporarilyUnavailable(profile))
            {
                ScheduleFolderAvailabilityRetry(profile.Id);
                if (_queues.TryGetValue(profile.Id, out PlaybackQueue? existingQueue) && existingQueue.Count > 0)
                {
                    return;
                }
            }
            else
            {
                StopFolderAvailabilityRetry(profile.Id);
            }

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
                HideWindow(profile.Id);
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

            if (result.LoadedFromCache)
            {
                DispatchFolderChange(profile.Id);
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

    public void RefreshFolder(string monitorId)
    {
        DispatchFolderChange(monitorId);
    }

    private async void ReloadChangedFolderAsync(string monitorId)
    {
        if (!_profiles.TryGetValue(monitorId, out MonitorProfile? profile) || string.IsNullOrWhiteSpace(profile.FolderPath))
        {
            return;
        }

        if (IsFolderTemporarilyUnavailable(profile))
        {
            ScheduleFolderAvailabilityRetry(profile.Id);
            return;
        }

        try
        {
            StopFolderAvailabilityRetry(profile.Id);
            IReadOnlyList<ImageMetadata> images = await _imageOrderService.ReloadOrderedImagesAsync(
                profile.FolderPath,
                profile.PlaybackOrder,
                profile.MediaFilter,
                profile.IncludeSubdirectories,
                CancellationToken.None);
            if (_autoTrackNewFiles)
            {
                ConfigureFolderWatcher(profile);
            }

            OrderedImagesChanged?.Invoke(this, new OrderedImagesChangedEventArgs(profile.Id, images));
            if (!_playbackEnabled || profile.IsStopped)
            {
                return;
            }

            bool hadWindow = _windows.ContainsKey(profile.Id);
            ReplaceQueueAfterCurrent(profile, images.Select(image => image.Path));
            if (!_queues.TryGetValue(profile.Id, out PlaybackQueue? queue) || queue.Count == 0)
            {
                HideWindow(profile.Id);
                return;
            }

            if (hadWindow && _windows.TryGetValue(profile.Id, out WallpaperWindow? existingWindow) && existingWindow.IsShowingWallpaper)
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

    private void RefreshFoldersAfterDisplayResume()
    {
        foreach (MonitorProfile profile in _profiles.Values)
        {
            if (!profile.IsStopped && !string.IsNullOrWhiteSpace(profile.FolderPath))
            {
                DispatchFolderChange(profile.Id);
            }
        }
    }

    private void ScheduleFolderAvailabilityRetry(string monitorId)
    {
        if (!_folderAvailabilityRetryTimers.TryGetValue(monitorId, out DispatcherQueueTimer? timer))
        {
            timer = _dispatcherQueue.CreateTimer();
            timer.Interval = FolderAvailabilityRetryDelay;
            timer.IsRepeating = true;
            timer.Tick += (_, _) => RetryUnavailableFolder(monitorId);
            _folderAvailabilityRetryTimers[monitorId] = timer;
        }

        timer.Start();
    }

    private void RetryUnavailableFolder(string monitorId)
    {
        if (!_profiles.TryGetValue(monitorId, out MonitorProfile? profile) || string.IsNullOrWhiteSpace(profile.FolderPath) || profile.IsStopped)
        {
            StopFolderAvailabilityRetry(monitorId);
            return;
        }

        if (IsFolderTemporarilyUnavailable(profile))
        {
            return;
        }

        StopFolderAvailabilityRetry(monitorId);
        DispatchFolderChange(monitorId);
    }

    private void StopFolderAvailabilityRetry(string monitorId)
    {
        if (_folderAvailabilityRetryTimers.Remove(monitorId, out DispatcherQueueTimer? timer))
        {
            timer.Stop();
        }
    }

    private void StopAllFolderAvailabilityRetries()
    {
        foreach (DispatcherQueueTimer timer in _folderAvailabilityRetryTimers.Values)
        {
            timer.Stop();
        }

        _folderAvailabilityRetryTimers.Clear();
    }

    private static bool IsFolderTemporarilyUnavailable(MonitorProfile profile)
    {
        return !string.IsNullOrWhiteSpace(profile.FolderPath) && !Directory.Exists(profile.FolderPath);
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
            PlaybackMediaFilter.PortraitImagesOnly => IsImageOrientation(path, requirePortrait: true),
            PlaybackMediaFilter.LandscapeImagesOnly => IsImageOrientation(path, requirePortrait: false),
            PlaybackMediaFilter.VideosOnly => ImageLibrary.IsSupportedVideoPath(path),
            _ => ImageLibrary.IsSupportedMediaPath(path),
        };
    }

    private static bool IsImageOrientation(string path, bool requirePortrait)
    {
        if (!ImageLibrary.IsSupportedImagePath(path))
        {
            return false;
        }

        int width;
        int height;
        if (NdfMediaService.TryGetMediaInfo(path, out NdfMediaInfo info))
        {
            width = info.Width;
            height = info.Height;
        }
        else if (!ImageDimensionReader.TryRead(path, out width, out height))
        {
            return false;
        }

        return requirePortrait ? height > width : width > height;
    }
}
