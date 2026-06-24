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
        if (!_previewItems.TryGetValue(args.MonitorId, out ObservableCollection<ImagePreviewItem>? items))
        {
            return;
        }

        ImagePreviewItem[] reusableItems = [.. items];
        ImagePreviewCollectionUpdater.Apply(items, args.Images, reusableItems, CreateThumbnailLoader());
        MonitorProfile? profile = _viewModel.Profiles.FirstOrDefault(item => string.Equals(item.Id, args.MonitorId, StringComparison.OrdinalIgnoreCase));
        if (profile is not null)
        {
            profile.TotalMediaCount = items.Count;
            UpdatePlaybackStatusText(profile);
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
        if (_backgroundStartupTrimPending && _settingsUiUnloadedForBackground)
        {
            ScheduleBackgroundMemoryTrim(BackgroundWallpaperReadyTrimDelay);
        }
    }

    private void Coordinator_HardwareOverlayMoved(object? sender, HardwareOverlayMovedEventArgs args)
    {
        _viewModel.HardwareMonitor.X = Math.Max(0, args.X);
        _viewModel.HardwareMonitor.Y = Math.Max(0, args.Y);
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
                ? previewItems.Select(previewItem => previewItem.Path).ToArray()
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
                items.Select(item => item.Metadata),
                PlaybackOrder.Random);
            _suppressPreviewSelection = true;
            try
            {
                ImagePreviewCollectionUpdater.Apply(items, shuffled, items, CreateThumbnailLoader());
            }
            finally
            {
                _suppressPreviewSelection = false;
            }
        }

        _coordinator.Shuffle(profile.Id);
    }

    private void RefreshProfileMedia(MonitorProfile profile)
    {
        _coordinator.RefreshFolder(profile.Id);
    }

    private void SaveCurrentImageCheckpoint()
    {
        _settingsStore.Save(CreateConfig());
    }
}
