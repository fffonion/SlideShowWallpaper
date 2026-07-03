using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using SlideShowWallpaper.ViewModels;

namespace SlideShowWallpaper;

public sealed partial class MainWindow
{
    private void Coordinator_OrderedImagesChanged(object? sender, OrderedImagesChangedEventArgs args)
    {
        MonitorProfile? profile = _viewModel.Profiles.FirstOrDefault(item => string.Equals(item.Id, args.MonitorId, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
        {
            ApplyMediaListCount(profile, args.Images);
        }

        if (_settingsUiUnloadedForBackground)
        {
            return;
        }

        if (!_previewItems.TryGetValue(args.MonitorId, out ObservableCollection<ImagePreviewItem>? items))
        {
            return;
        }

        ImagePreviewItem[] reusableItems = [.. items];
        if (profile is not null)
        {
            ApplyPreviewItems(profile, items, args.Images, reusableItems);
        }
        else
        {
            ImagePreviewCollectionUpdater.Apply(items, args.Images, reusableItems, CreateThumbnailLoader());
        }
    }

    private void Coordinator_CurrentWallpaperChanged(object? sender, CurrentWallpaperChangedEventArgs args)
    {
        _ = CurrentWallpaperSelectionUpdater.Update(_viewModel.Profiles, args.MonitorId, args.ImagePath);
        MonitorProfile? profile = _viewModel.Profiles.FirstOrDefault(item => string.Equals(item.Id, args.MonitorId, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        profile.CurrentMediaIndex = args.CurrentIndex;
        profile.TotalMediaCount = args.TotalCount;
        profile.CurrentMediaStartedAt = DateTimeOffset.Now;
        UpdatePlaybackStatusText(profile);
        MoveCurrentPreviewItemToFront(profile);
        if (_backgroundStartupTrimPending && _settingsUiUnloadedForBackground)
        {
            ScheduleBackgroundMemoryTrim(BackgroundWallpaperReadyTrimDelay);
        }
    }

    private void Coordinator_HardwareOverlayMoved(object? sender, HardwareOverlayMovedEventArgs args)
    {
        _viewModel.HardwareMonitor.X = HardwareEditorLayoutService.QuantizeCoordinate(args.X, double.MaxValue);
        _viewModel.HardwareMonitor.Y = HardwareEditorLayoutService.QuantizeCoordinate(args.Y, double.MaxValue);
        _settingsStore.Save(CreateConfig());
    }

    private async void PreviewList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressPreviewSelection)
        {
            return;
        }

        if (sender is not ListView { Tag: MonitorProfile profile, SelectedItem: ImagePreviewItem item })
        {
            return;
        }

        profile.SelectedImagePath = item.Path;
        ApplySettings();
        try
        {
            IReadOnlyList<string> orderedPaths = sender is ListView { ItemsSource: IEnumerable<ImagePreviewItem> previewItems }
                ? GetOrderedPreviewPaths(profile, previewItems)
                : [];
            await _coordinator.ShowImageAsync(profile, item.Path, orderedPaths);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private void TogglePauseFromTray(string monitorId)
    {
        MonitorProfile? profile = _viewModel.Profiles.FirstOrDefault(item => string.Equals(item.Id, monitorId, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        TogglePause(profile, !profile.IsPaused);
        RenderTabs(profile.Id);
    }

    private void ToggleStopFromTray(string monitorId)
    {
        MonitorProfile? profile = _viewModel.Profiles.FirstOrDefault(item => string.Equals(item.Id, monitorId, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            return;
        }

        ToggleStop(profile, !profile.IsStopped);
        RenderTabs(profile.Id);
    }

    private void NextFromTray(string monitorId)
    {
        _ = _coordinator.ShowNextAsync(monitorId);
    }

    private void TogglePause(MonitorProfile profile, bool isPaused)
    {
        profile.IsPaused = isPaused;
        _coordinator.PauseOrResume(profile.Id, profile.IsPaused);
        ApplySettings();
    }

    private void ToggleStop(MonitorProfile profile, bool isStopped)
    {
        profile.IsStopped = isStopped;
        ApplySettings();
    }

    private async Task ShowNextAsync(MonitorProfile profile)
    {
        if (profile.IsStopped)
        {
            profile.IsStopped = false;
            ApplySettings();
        }

        await _coordinator.ShowNextAsync(profile.Id);
    }

    private void ShuffleProfile(MonitorProfile profile)
    {
        if (profile.PlaybackOrder != PlaybackOrder.Random)
        {
            return;
        }

        if (_previewItems.TryGetValue(profile.Id, out ObservableCollection<ImagePreviewItem>? items) && items.Count > 1)
        {
            IReadOnlyList<ImageMetadata> shuffled = ImageLibrary.SortImages(
                GetOrderedPreviewMetadata(profile, items),
                PlaybackOrder.Random);
            _previewOrderedImages[profile.Id] = shuffled;
            _suppressPreviewSelection = true;
            try
            {
                ImagePreviewCollectionUpdater.ApplyWithFirstItem(items, shuffled, items, CreateThumbnailLoader(), profile.SelectedImagePath);
                SelectCurrentPreviewItem(profile, scrollIntoView: false);
            }
            finally
            {
                _suppressPreviewSelection = false;
            }
        }

        _coordinator.Shuffle(profile.Id);
    }

    private IEnumerable<ImageMetadata> GetOrderedPreviewMetadata(MonitorProfile profile, IEnumerable<ImagePreviewItem> fallbackItems)
    {
        return _previewOrderedImages.TryGetValue(profile.Id, out IReadOnlyList<ImageMetadata>? images)
            ? images
            : fallbackItems.Select(item => item.Metadata);
    }

    private void RefreshProfileMedia(MonitorProfile profile)
    {
        _coordinator.RefreshFolder(profile.Id);
    }

    private void StartBackgroundMediaListLoad()
    {
        foreach (MonitorProfile profile in _viewModel.Profiles)
        {
            _ = LoadBackgroundMediaListAsync(profile);
        }
    }

    private async Task LoadBackgroundMediaListAsync(MonitorProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FolderPath))
        {
            ApplyMediaListCount(profile, []);
            return;
        }

        try
        {
            ImageOrderLoadResult result = await _imageOrderService.GetOrLoadOrderedImagesWithStatusAsync(
                profile.FolderPath,
                profile.PlaybackOrder,
                profile.MediaFilter,
                profile.IncludeSubdirectories,
                CancellationToken.None);
            ApplyMediaListCount(profile, result.Images);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private void ApplyMediaListCount(MonitorProfile profile, IReadOnlyList<ImageMetadata> images)
    {
        profile.TotalMediaCount = images.Count;
        profile.CurrentMediaIndex = ResolveCurrentMediaIndex(profile, images);
        UpdatePlaybackStatusText(profile);
    }

    private static int ResolveCurrentMediaIndex(MonitorProfile profile, IReadOnlyList<ImageMetadata> images)
    {
        if (images.Count == 0)
        {
            return 0;
        }

        if (profile.CurrentMediaIndex >= 1 && profile.CurrentMediaIndex <= images.Count)
        {
            return profile.CurrentMediaIndex;
        }

        if (!string.IsNullOrWhiteSpace(profile.SelectedImagePath))
        {
            for (int index = 0; index < images.Count; index++)
            {
                if (string.Equals(images[index].Path, profile.SelectedImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    return index + 1;
                }
            }
        }

        return 0;
    }

    private void SaveCurrentImageCheckpoint()
    {
        _settingsStore.Save(CreateConfig());
    }
}
