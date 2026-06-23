using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using SlideShowWallpaper.ViewModels;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace SlideShowWallpaper;

public sealed partial class MainWindow
{
    private TextBlock CreatePreviewMetadataText(MonitorProfile profile)
    {
        var metadata = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(profile.FolderPath) ? LocalizedStrings.Get("ImageCountZero") : FormatPreviewStatusText(profile),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _previewMetadataTexts[profile.Id] = metadata;
        AutomationProperties.SetName(metadata, LocalizedStrings.Format("MonitorImageCountAutomationFormat", profile.DisplayName));
        return metadata;
    }

    private FrameworkElement BuildPreviewPane(MonitorProfile profile, TextBlock metadata)
    {
        ObservableCollection<ImagePreviewItem> items = GetPreviewItems(profile);
        var previewHost = new Grid();
        var previewList = new ListView
        {
            ItemsSource = items,
            ItemTemplate = CreatePreviewTemplate(),
            SelectionMode = ListViewSelectionMode.Single,
            Tag = profile,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        AutomationProperties.SetName(previewList, LocalizedStrings.Format("MonitorPreviewsAutomationFormat", profile.DisplayName));
        previewList.ContainerContentChanging += PreviewList_ContainerContentChanging;
        previewList.SelectionChanged += PreviewList_SelectionChanged;
        previewHost.Children.Add(previewList);

        var loadingPanel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8,
            Visibility = string.IsNullOrWhiteSpace(profile.FolderPath) ? Visibility.Collapsed : Visibility.Visible,
        };
        var progressRing = new ProgressRing
        {
            IsActive = !string.IsNullOrWhiteSpace(profile.FolderPath),
            Width = 32,
            Height = 32,
        };
        AutomationProperties.SetName(progressRing, LocalizedStrings.Get("LoadingImagePreviews"));
        loadingPanel.Children.Add(progressRing);
        loadingPanel.Children.Add(new TextBlock
        {
            Text = LocalizedStrings.Get("LoadingImages"),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        previewHost.Children.Add(loadingPanel);

        StartPreviewLoad(profile, items, metadata, loadingPanel, progressRing);

        return previewHost;
    }

    private static DataTemplate CreatePreviewTemplate()
    {
        return ImagePreviewTemplateFactory.Create();
    }

    private void PreviewList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not ListViewItem itemContainer)
        {
            return;
        }

        if (ReferenceEquals(itemContainer, _previewPopupPendingContainer) && !ReferenceEquals(args.Item, _previewPopupPendingItem))
        {
            CancelPreviewPopup();
        }

        itemContainer.Tag = args.Item;
        itemContainer.PointerEntered -= PreviewItem_PointerEntered;
        itemContainer.PointerExited -= PreviewItem_PointerExited;
        itemContainer.Unloaded -= PreviewItem_Unloaded;
        itemContainer.PointerEntered += PreviewItem_PointerEntered;
        itemContainer.PointerExited += PreviewItem_PointerExited;
        itemContainer.Unloaded += PreviewItem_Unloaded;
    }

    private void PreviewItem_PointerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (sender is not ListViewItem { Tag: ImagePreviewItem item } itemContainer || SelectedProfile is not { } profile)
        {
            return;
        }

        _previewPopupTimer.Stop();
        UpdatePreviewPopupDelay();
        HidePreviewPopup();
        _previewPopupPendingItem = item;
        _previewPopupPendingContainer = itemContainer;
        _previewPopupPendingProfile = profile;
        _previewPopupTimer.Start();
    }

    private void PreviewItem_PointerExited(object sender, PointerRoutedEventArgs args)
    {
        if (ReferenceEquals(sender, _previewPopupPendingContainer))
        {
            CancelPreviewPopup();
        }
    }

    private void PreviewItem_Unloaded(object sender, RoutedEventArgs args)
    {
        if (ReferenceEquals(sender, _previewPopupPendingContainer))
        {
            CancelPreviewPopup();
        }
    }

    private async void PreviewPopupTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (_previewPopupPendingItem is not { } item
            || _previewPopupPendingContainer is not { } itemContainer
            || _previewPopupPendingProfile is not { } profile)
        {
            return;
        }

        await ShowPreviewPopupAsync(item, itemContainer, profile);
    }

    private async Task ShowPreviewPopupAsync(ImagePreviewItem item, ListViewItem itemContainer, MonitorProfile profile)
    {
        if (!File.Exists(item.Path) || itemContainer.XamlRoot is null)
        {
            return;
        }

        CancellationTokenSource cancellation = ReplacePreviewPopupCancellation();
        try
        {
            string playbackPath = await NdfMediaService.MaterializeForPlaybackAsync(item.Path, cancellation.Token);
            if (cancellation.IsCancellationRequested
                || !ReferenceEquals(item, _previewPopupPendingItem)
                || !ReferenceEquals(itemContainer, _previewPopupPendingContainer))
            {
                return;
            }

            EnsurePreviewPopup();
            PreviewPopupMediaLayout mediaSize = await GetPreviewPopupMediaSizeAsync(item.Path, playbackPath, item.Metadata.Kind, cancellation.Token);
            ConfigurePreviewPopupSurface(mediaSize.Width, mediaSize.Height);
            if (item.Metadata.Kind == MediaKind.Video)
            {
                await ShowPreviewPopupVideoAsync(playbackPath, profile, mediaSize, cancellation.Token);
            }
            else
            {
                ShowPreviewPopupImage(playbackPath);
            }

            PositionPreviewPopup(itemContainer);
            if (_previewPopup is not null)
            {
                _previewPopup.XamlRoot = Root.XamlRoot;
                _previewPopup.IsOpen = true;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            HidePreviewPopup();
        }
        finally
        {
            if (ReferenceEquals(_previewPopupCancellation, cancellation))
            {
                _previewPopupCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private CancellationTokenSource ReplacePreviewPopupCancellation()
    {
        _previewPopupCancellation?.Cancel();
        _previewPopupCancellation = new CancellationTokenSource();
        return _previewPopupCancellation;
    }

    private void EnsurePreviewPopup()
    {
        if (_previewPopup is not null)
        {
            return;
        }

        _previewPopupPlayer = new MediaPlayer
        {
            IsLoopingEnabled = true,
            IsMuted = true,
        };
        _previewPopupPlayer.MediaOpened += PreviewPopupPlayer_MediaOpened;
        _previewPopupPlayer.MediaFailed += PreviewPopupPlayer_MediaFailed;
        _previewPopupImage = new Microsoft.UI.Xaml.Controls.Image
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        AutomationProperties.SetAccessibilityView(_previewPopupImage, AccessibilityView.Raw);
        _previewPopupVideo = new MediaPlayerElement
        {
            AreTransportControlsEnabled = false,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Visibility = Visibility.Collapsed,
        };
        _previewPopupVideo.SetMediaPlayer(_previewPopupPlayer);
        _previewPopupVideoFrame = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            Child = _previewPopupVideo,
        };
        SetPreviewPopupVideoFrameSize(16, 9);

        var content = new Grid();
        content.Children.Add(_previewPopupImage);
        content.Children.Add(_previewPopupVideoFrame);

        _previewPopupSurface = new Border
        {
            Width = PreviewPopupWidth,
            Height = PreviewPopupHeight,
            Padding = new Thickness(PreviewPopupPadding),
            CornerRadius = new CornerRadius(8),
            Background = GetThemeBrush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = GetThemeBrush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(PreviewPopupBorderThickness),
            Child = content,
        };

        _previewPopup = new Popup
        {
            Child = _previewPopupSurface,
            IsLightDismissEnabled = false,
        };
    }

    private async Task ShowPreviewPopupVideoAsync(string playbackPath, MonitorProfile profile, PreviewPopupMediaLayout mediaSize, CancellationToken cancellationToken)
    {
        if (_previewPopupPlayer is null || _previewPopupVideo is null || _previewPopupImage is null)
        {
            return;
        }

        StorageFile file = await StorageFile.GetFileFromPathAsync(playbackPath).AsTask(cancellationToken);
        _previewPopupImage.Source = null;
        _previewPopupImage.Visibility = Visibility.Collapsed;
        if (_previewPopupVideoFrame is not null)
        {
            _previewPopupVideoFrame.Visibility = Visibility.Visible;
        }

        _previewPopupVideo.Visibility = Visibility.Visible;
        SetPreviewPopupVideoFrameSize(mediaSize.Width, mediaSize.Height);
        _previewPopupPlayer.IsLoopingEnabled = true;
        _previewPopupPlayer.IsMuted = PreviewPopupPolicy.ShouldMuteVideo(_viewModel.GlobalMute, profile);
        _previewPopupPlayer.Source = MediaSource.CreateFromStorageFile(file);
        _previewPopupPlayer.Play();
    }

    private void ShowPreviewPopupImage(string playbackPath)
    {
        if (_previewPopupPlayer is null || _previewPopupVideo is null || _previewPopupImage is null)
        {
            return;
        }

        _previewPopupPlayer.Pause();
        _previewPopupPlayer.Source = null;
        if (_previewPopupVideoFrame is not null)
        {
            _previewPopupVideoFrame.Visibility = Visibility.Collapsed;
        }

        _previewPopupVideo.Visibility = Visibility.Collapsed;
        _previewPopupImage.Source = new BitmapImage(new Uri(playbackPath));
        _previewPopupImage.Visibility = Visibility.Visible;
    }

    private void PositionPreviewPopup(FrameworkElement anchor)
    {
        if (_previewPopup is null)
        {
            return;
        }

        Point anchorPoint = anchor.TransformToVisual(Root).TransformPoint(new Point());
        double rootWidth = Root.ActualWidth > 0 ? Root.ActualWidth : AppWindow.Size.Width;
        double rootHeight = Root.ActualHeight > 0 ? Root.ActualHeight : AppWindow.Size.Height;
        double x = anchorPoint.X + anchor.ActualWidth + PreviewPopupGap;
        if (x + _previewPopupCurrentWidth > rootWidth)
        {
            x = Math.Max(0, anchorPoint.X - _previewPopupCurrentWidth - PreviewPopupGap);
        }

        double y = Math.Clamp(anchorPoint.Y, 0, Math.Max(0, rootHeight - _previewPopupCurrentHeight));
        _previewPopup.HorizontalOffset = x;
        _previewPopup.VerticalOffset = y;
    }

    private void PreviewPopupPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        string errorMessage = string.IsNullOrWhiteSpace(args.ErrorMessage)
            ? args.Error.ToString()
            : $"{args.Error}: {args.ErrorMessage}";
        AppLog.Write($"Preview media failed: {errorMessage}");
    }

    private void PreviewPopupPlayer_MediaOpened(MediaPlayer sender, object args)
    {
        double width = sender.PlaybackSession.NaturalVideoWidth;
        double height = sender.PlaybackSession.NaturalVideoHeight;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ConfigurePreviewPopupSurface(width, height);
            SetPreviewPopupVideoFrameSize(width, height);
            if (_previewPopupPendingContainer is not null)
            {
                PositionPreviewPopup(_previewPopupPendingContainer);
            }
        });
    }

    private static async Task<PreviewPopupMediaLayout> GetPreviewPopupMediaSizeAsync(
        string sourcePath,
        string playbackPath,
        MediaKind kind,
        CancellationToken cancellationToken)
    {
        if (NdfMediaService.TryGetMediaInfo(sourcePath, out NdfMediaInfo ndfInfo)
            && ndfInfo.Width > 0
            && ndfInfo.Height > 0)
        {
            return new PreviewPopupMediaLayout(ndfInfo.Width, ndfInfo.Height);
        }

        StorageFile file = await StorageFile.GetFileFromPathAsync(playbackPath).AsTask(cancellationToken);
        if (kind == MediaKind.Video)
        {
            global::Windows.Storage.FileProperties.VideoProperties properties = await file.Properties.GetVideoPropertiesAsync().AsTask(cancellationToken);
            return new PreviewPopupMediaLayout(properties.Width, properties.Height);
        }

        using global::Windows.Storage.Streams.IRandomAccessStream stream = await file.OpenReadAsync().AsTask(cancellationToken);
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken);
        return new PreviewPopupMediaLayout(decoder.PixelWidth, decoder.PixelHeight);
    }

    private void ConfigurePreviewPopupSurface(double mediaWidth, double mediaHeight)
    {
        PreviewPopupSurfaceLayout layout = PreviewPopupLayoutCalculator.CalculateSurface(
            mediaWidth,
            mediaHeight,
            PreviewPopupWidth,
            PreviewPopupHeight);
        _previewPopupCurrentWidth = layout.Width;
        _previewPopupCurrentHeight = layout.Height;

        if (_previewPopupSurface is not null)
        {
            _previewPopupSurface.Width = layout.Width;
            _previewPopupSurface.Height = layout.Height;
        }
    }

    private void SetPreviewPopupVideoFrameSize(double mediaWidth, double mediaHeight)
    {
        if (_previewPopupVideoFrame is null)
        {
            return;
        }

        PreviewPopupMediaLayout layout = PreviewPopupLayoutCalculator.Calculate(
            mediaWidth,
            mediaHeight,
            _previewPopupCurrentWidth - ((PreviewPopupPadding + PreviewPopupBorderThickness) * 2),
            _previewPopupCurrentHeight - ((PreviewPopupPadding + PreviewPopupBorderThickness) * 2));
        _previewPopupVideoFrame.Width = layout.Width;
        _previewPopupVideoFrame.Height = layout.Height;
        _previewPopupVideoFrame.Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, layout.Width, layout.Height),
        };
    }

    private void CancelPreviewPopup()
    {
        _previewPopupTimer.Stop();
        _previewPopupPendingItem = null;
        _previewPopupPendingContainer = null;
        _previewPopupPendingProfile = null;
        HidePreviewPopup();
    }

    private void HidePreviewPopup()
    {
        _previewPopupCancellation?.Cancel();
        _previewPopupCancellation = null;
        if (_previewPopupPlayer is not null)
        {
            _previewPopupPlayer.Pause();
            _previewPopupPlayer.Source = null;
        }

        if (_previewPopupImage is not null)
        {
            _previewPopupImage.Source = null;
        }

        if (_previewPopupVideo is not null)
        {
            _previewPopupVideo.Visibility = Visibility.Collapsed;
        }

        if (_previewPopupVideoFrame is not null)
        {
            _previewPopupVideoFrame.Visibility = Visibility.Collapsed;
        }

        if (_previewPopup is not null)
        {
            _previewPopup.IsOpen = false;
        }

        ConfigurePreviewPopupSurface(16, 9);
    }

    private void UpdatePreviewPopupMute()
    {
        if (_previewPopupPlayer is not null && _previewPopupPendingProfile is not null)
        {
            _previewPopupPlayer.IsMuted = PreviewPopupPolicy.ShouldMuteVideo(_viewModel.GlobalMute, _previewPopupPendingProfile);
        }
    }

    private void UpdatePreviewPopupDelay()
    {
        _previewPopupTimer.Interval = PreviewPopupPolicy.GetHoverDelay(_viewModel.PreviewPopupDelaySeconds);
    }

    private void DisposePreviewPopup()
    {
        CancelPreviewPopup();
        if (_previewPopupPlayer is not null)
        {
            _previewPopupPlayer.MediaOpened -= PreviewPopupPlayer_MediaOpened;
            _previewPopupPlayer.MediaFailed -= PreviewPopupPlayer_MediaFailed;
            _previewPopupPlayer.Dispose();
            _previewPopupPlayer = null;
        }

        _previewPopupVideoFrame = null;
        _previewPopupVideo = null;
        _previewPopupImage = null;
        _previewPopupSurface = null;
        _previewPopup = null;
    }

    private ObservableCollection<ImagePreviewItem> GetPreviewItems(MonitorProfile profile)
    {
        if (_previewItems.TryGetValue(profile.Id, out ObservableCollection<ImagePreviewItem>? existing))
        {
            return existing;
        }

        var items = new ObservableCollection<ImagePreviewItem>();
        _previewItems[profile.Id] = items;
        return items;
    }

    private async void StartPreviewLoad(
        MonitorProfile profile,
        ObservableCollection<ImagePreviewItem> items,
        TextBlock metadataText,
        FrameworkElement loadingPanel,
        ProgressRing progressRing)
    {
        if (string.IsNullOrWhiteSpace(profile.FolderPath))
        {
            metadataText.Text = LocalizedStrings.Get("ImageCountZero");
            loadingPanel.Visibility = Visibility.Collapsed;
            progressRing.IsActive = false;
            return;
        }

        CancelPreviewLoad(profile.Id);
        var cancellation = new CancellationTokenSource();
        _previewLoadTokens[profile.Id] = cancellation;
        int previewSessionVersion = _previewSessionVersion;
        metadataText.Text = LocalizedStrings.Get("LoadingImages");
        loadingPanel.Visibility = Visibility.Visible;
        progressRing.IsActive = true;
        ImagePreviewItem[] reusableItems = [.. items];
        items.Clear();

        try
        {
            IReadOnlyList<ImageMetadata> images = await _imageOrderService.GetOrLoadOrderedImagesAsync(profile.FolderPath, profile.PlaybackOrder, profile.MediaFilter, cancellation.Token);
            if (IsPreviewLoadExpired(profile.Id, cancellation, previewSessionVersion))
            {
                return;
            }

            ImagePreviewCollectionUpdater.Apply(items, images, reusableItems, CreateThumbnailLoader());

            profile.TotalMediaCount = items.Count;
            UpdatePlaybackStatusText(profile);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (IsPreviewLoadExpired(profile.Id, cancellation, previewSessionVersion))
            {
                return;
            }

            AppLog.Write(exception);
            items.Clear();
            metadataText.Text = LocalizedStrings.Get("UnableToLoadImages");
        }
        finally
        {
            if (!IsPreviewLoadExpired(profile.Id, cancellation, previewSessionVersion))
            {
                _previewLoadTokens.Remove(profile.Id);
                loadingPanel.Visibility = Visibility.Collapsed;
                progressRing.IsActive = false;
            }

            cancellation.Dispose();
        }
    }

    private void CancelPreviewLoad(string monitorId)
    {
        if (_previewLoadTokens.Remove(monitorId, out CancellationTokenSource? cancellation))
        {
            cancellation.Cancel();
        }
    }

    private void CancelSelectedPreviewLoad()
    {
        CancelPreviewPopup();
        if (!string.IsNullOrWhiteSpace(_selectedMonitorId))
        {
            CancelPreviewLoad(_selectedMonitorId);
        }
    }

    private bool IsPreviewLoadExpired(string monitorId, CancellationTokenSource cancellation, int previewSessionVersion)
    {
        return cancellation.IsCancellationRequested
            || previewSessionVersion != _previewSessionVersion
            || !_previewLoadTokens.TryGetValue(monitorId, out CancellationTokenSource? current)
            || !ReferenceEquals(current, cancellation);
    }

    private void UnloadPreviewState()
    {
        CancelPreviewPopup();
        foreach (ObservableCollection<ImagePreviewItem> items in _previewItems.Values)
        {
            ImagePreviewCollectionUpdater.Clear(items);
        }

        _previewItems.Clear();
        _previewMetadataTexts.Clear();
    }

    private void UpdateAllPlaybackStatusTexts()
    {
        foreach (MonitorProfile profile in _viewModel.Profiles)
        {
            UpdatePlaybackStatusText(profile);
        }
    }

    private void UpdatePlaybackStatusText(MonitorProfile profile)
    {
        if (_previewMetadataTexts.TryGetValue(profile.Id, out TextBlock? metadataText))
        {
            metadataText.Text = FormatPreviewStatusText(profile);
        }
    }

    private static string FormatPreviewStatusText(MonitorProfile profile)
    {
        if (profile.PlaybackOrder == PlaybackOrder.SingleLoop)
        {
            return PlaybackStatusFormatter.FormatPreviewStatusWithoutRemaining(profile.CurrentMediaIndex, profile.TotalMediaCount);
        }

        int remainingSeconds = PlaybackStatusFormatter.CalculateLoopRemainingSeconds(
            profile.CurrentMediaIndex,
            profile.TotalMediaCount,
            profile.IntervalSeconds,
            profile.CurrentMediaStartedAt,
            DateTimeOffset.Now,
            profile.PlaybackOrder);
        return PlaybackStatusFormatter.FormatPreviewStatus(profile.CurrentMediaIndex, profile.TotalMediaCount, remainingSeconds);
    }
}
