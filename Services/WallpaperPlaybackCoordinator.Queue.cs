using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed partial class WallpaperPlaybackCoordinator
{
    private void ConfigureFolderWatcher(MonitorProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FolderPath))
        {
            _folderChangeWatcherService.Unwatch(profile.Id);
            return;
        }

        _folderChangeWatcherService.Watch(profile.Id, profile.FolderPath, () => DispatchFolderChange(profile.Id));
    }

    private async void StartRebuildQueue(MonitorProfile profile)
    {
        int version = _queueVersions.TryGetValue(profile.Id, out int currentVersion) ? currentVersion + 1 : 1;
        _queueVersions[profile.Id] = version;
        bool selectedImageShown = false;
        try
        {
            selectedImageShown = await TryShowSelectedImageAsync(profile);
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
}