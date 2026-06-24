using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using WinRT.Interop;

namespace SlideShowWallpaper.Windows;

public sealed partial class WallpaperWindow : Window
{
    private readonly TranslateTransform _currentTransform = new();
    private readonly TranslateTransform _nextTransform = new();
    private readonly TranslateTransform _videoTransform = new();
    private readonly IntPtr _hwnd;
    private MediaPlayer _mediaPlayer;
    private MonitorProfile _profile;
    private string _currentImagePath = string.Empty;
    private MediaKind _currentKind = MediaKind.Image;
    private int _mediaRequestVersion;
    private bool _isClosed;
    private bool _isShowingWallpaper;
    private bool _videoPausedByCoverage;
    private bool _forceMuted;
    private bool _isHardwareOverlayDragging;
    private Point _hardwareOverlayDragStartPoint;
    private double _hardwareOverlayDragStartX;
    private double _hardwareOverlayDragStartY;

    public WallpaperWindow(MonitorProfile profile)
    {
        _profile = profile;
        InitializeComponent();
        Title = LocalizedStrings.Get("PlayerTitle");
        SystemBackdrop = null;
        _hwnd = WindowNative.GetWindowHandle(this);
        CurrentImage.RenderTransform = _currentTransform;
        NextImage.RenderTransform = _nextTransform;
        VideoPlayer.RenderTransform = _videoTransform;
        _mediaPlayer = CreateMediaPlayer(profile.VideoLoop);
        VideoPlayer.SetMediaPlayer(_mediaPlayer);
        ConfigureWindow();
        ConfigureHardwareOverlayDrag();
        ApplyProfile(profile);
        Root.SizeChanged += (_, _) => ApplyProfile(_profile);
        Closed += (_, _) =>
        {
            _isClosed = true;
            StopVideo();
            ClearImageSources();
            DisposeMediaPlayer(_mediaPlayer);
        };
    }

    public event EventHandler? VideoEnded;

    public event EventHandler<HardwareOverlayMovedEventArgs>? HardwareOverlayMoved;

    public bool IsShowingWallpaper => _isShowingWallpaper;

    public void ApplyProfile(MonitorProfile profile)
    {
        _profile = profile;
        CurrentImage.Stretch = Stretch.Fill;
        NextImage.Stretch = Stretch.Fill;
        VideoPlayer.Stretch = Stretch.Fill;
        _mediaPlayer.IsLoopingEnabled = VideoPlaybackPolicy.ShouldLoopVideo(profile);
        ApplyMute(profile);
        ApplyImageLayout(CurrentImage, _currentTransform, profile);
        ApplyImageLayout(NextImage, _nextTransform, profile);
        ApplyVideoLayout(profile);
    }

    public void SetForceMuted(bool forceMuted)
    {
        _forceMuted = forceMuted;
        ApplyMute(_profile);
    }

    public void ShowWallpaperWindow()
    {
        if (_isClosed)
        {
            return;
        }

        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
    }

    public void HideWallpaperWindow()
    {
        if (_isClosed)
        {
            return;
        }

        StopVideo();
        ClearImageSources();
        HideError();
        SetHardwareOverlay(new HardwareOverlayState(false, string.Empty, [], 0, 0, 0, 0));
        _isShowingWallpaper = false;
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
    }

    public void SetHardwareOverlay(HardwareOverlayState state)
    {
        if (!state.IsVisible)
        {
            HardwareOverlay.Visibility = Visibility.Collapsed;
            HardwareOverlayBackground.Source = null;
            HardwareOverlayBackground.Visibility = Visibility.Collapsed;
            HardwareOverlayCanvas.Children.Clear();
            HardwareOverlayCanvas.Visibility = Visibility.Collapsed;
            HardwareOverlayContent.Children.Clear();
            return;
        }

        double fontSize = Math.Max(10, state.FontSize);
        if (state.Elements.Count > 0 || !string.IsNullOrWhiteSpace(state.BackgroundImagePath))
        {
            RenderHardwareOverlayCanvas(state, fontSize);
            return;
        }

        HardwareOverlay.Padding = new Thickness(10, 8, 10, 8);
        HardwareOverlay.Width = double.NaN;
        HardwareOverlay.Height = double.NaN;
        HardwareOverlayCanvas.Children.Clear();
        HardwareOverlayCanvas.Visibility = Visibility.Collapsed;
        HardwareOverlayBackground.Source = null;
        HardwareOverlayBackground.Visibility = Visibility.Collapsed;
        HardwareOverlayContent.Visibility = Visibility.Visible;
        HardwareOverlayContent.Children.Clear();
        if (!string.IsNullOrWhiteSpace(state.Text))
        {
            HardwareOverlayContent.Children.Add(CreateHardwareOverlayText(state.Text, fontSize));
        }

        foreach (HardwareOverlayMetric metric in state.Metrics)
        {
            HardwareOverlayContent.Children.Add(CreateHardwareMetricRow(metric, fontSize));
        }

        HardwareOverlay.Opacity = Math.Clamp(state.Opacity, 0.1, 1);
        SetHardwareOverlayPosition(state.X, state.Y);
        HardwareOverlay.Visibility = Visibility.Visible;
    }

    private void RenderHardwareOverlayCanvas(HardwareOverlayState state, double fontSize)
    {
        HardwareOverlay.Padding = new Thickness(0);
        HardwareOverlayContent.Children.Clear();
        HardwareOverlayContent.Visibility = Visibility.Collapsed;
        HardwareOverlayCanvas.Children.Clear();

        double width = Math.Max(300, state.Elements.Count == 0 ? 300 : state.Elements.Max(element => element.X + element.Width) + 16);
        double height = Math.Max(160, state.Elements.Count == 0 ? 160 : state.Elements.Max(element => element.Y + element.Height) + 16);
        HardwareOverlay.Width = width;
        HardwareOverlay.Height = height;
        HardwareOverlayRoot.Width = width;
        HardwareOverlayRoot.Height = height;
        HardwareOverlayCanvas.Width = width;
        HardwareOverlayCanvas.Height = height;
        HardwareOverlayCanvas.Visibility = Visibility.Visible;

        if (TryCreateBitmapImage(state.BackgroundImagePath, out BitmapImage? background))
        {
            HardwareOverlayBackground.Source = background;
            HardwareOverlayBackground.Width = width;
            HardwareOverlayBackground.Height = height;
            HardwareOverlayBackground.Visibility = Visibility.Visible;
        }
        else
        {
            HardwareOverlayBackground.Source = null;
            HardwareOverlayBackground.Visibility = Visibility.Collapsed;
        }

        if (state.Elements.Count == 0 && (!string.IsNullOrWhiteSpace(state.Text) || state.Metrics.Count > 0))
        {
            var legacyContent = new StackPanel
            {
                Spacing = 5,
                Padding = new Thickness(10, 8, 10, 8),
            };
            if (!string.IsNullOrWhiteSpace(state.Text))
            {
                legacyContent.Children.Add(CreateHardwareOverlayText(state.Text, fontSize));
            }

            foreach (HardwareOverlayMetric metric in state.Metrics)
            {
                legacyContent.Children.Add(CreateHardwareMetricRow(metric, fontSize));
            }

            Canvas.SetLeft(legacyContent, 0);
            Canvas.SetTop(legacyContent, 0);
            HardwareOverlayCanvas.Children.Add(legacyContent);
        }

        foreach (HardwareOverlayElementState element in state.Elements)
        {
            UIElement visual = CreateHardwareOverlayElement(element, fontSize);
            Canvas.SetLeft(visual, element.X);
            Canvas.SetTop(visual, element.Y);
            HardwareOverlayCanvas.Children.Add(visual);
        }

        HardwareOverlay.Opacity = Math.Clamp(state.Opacity, 0.1, 1);
        SetHardwareOverlayPosition(state.X, state.Y);
        HardwareOverlay.Visibility = Visibility.Visible;
    }

    private void ConfigureHardwareOverlayDrag()
    {
        HardwareOverlay.PointerPressed += HardwareOverlay_PointerPressed;
        HardwareOverlay.PointerMoved += HardwareOverlay_PointerMoved;
        HardwareOverlay.PointerReleased += HardwareOverlay_PointerReleased;
        HardwareOverlay.PointerCanceled += HardwareOverlay_PointerCanceled;
        HardwareOverlay.PointerCaptureLost += HardwareOverlay_PointerCaptureLost;
    }

    private void HardwareOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (HardwareOverlay.Visibility != Visibility.Visible)
        {
            return;
        }

        _isHardwareOverlayDragging = true;
        _hardwareOverlayDragStartPoint = e.GetCurrentPoint(Root).Position;
        _hardwareOverlayDragStartX = HardwareOverlay.Margin.Left;
        _hardwareOverlayDragStartY = HardwareOverlay.Margin.Top;
        HardwareOverlay.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void HardwareOverlay_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isHardwareOverlayDragging)
        {
            return;
        }

        Point currentPoint = e.GetCurrentPoint(Root).Position;
        SetHardwareOverlayPosition(
            _hardwareOverlayDragStartX + currentPoint.X - _hardwareOverlayDragStartPoint.X,
            _hardwareOverlayDragStartY + currentPoint.Y - _hardwareOverlayDragStartPoint.Y);
        e.Handled = true;
    }

    private void HardwareOverlay_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        CompleteHardwareOverlayDrag(true, e);
        e.Handled = true;
    }

    private void HardwareOverlay_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        CompleteHardwareOverlayDrag(false, e);
    }

    private void HardwareOverlay_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        CompleteHardwareOverlayDrag(true, e);
    }

    private void CompleteHardwareOverlayDrag(bool notify, PointerRoutedEventArgs e)
    {
        if (!_isHardwareOverlayDragging)
        {
            return;
        }

        _isHardwareOverlayDragging = false;
        try
        {
            HardwareOverlay.ReleasePointerCapture(e.Pointer);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }

        if (notify)
        {
            HardwareOverlayMoved?.Invoke(this, new HardwareOverlayMovedEventArgs(HardwareOverlay.Margin.Left, HardwareOverlay.Margin.Top));
        }
    }

    private void SetHardwareOverlayPosition(double x, double y)
    {
        double clampedX = ClampHardwareOverlayCoordinate(x, GetViewportWidth(Root), GetOverlayWidth());
        double clampedY = ClampHardwareOverlayCoordinate(y, GetViewportHeight(Root), GetOverlayHeight());
        HardwareOverlay.Margin = new Thickness(clampedX, clampedY, 0, 0);
    }

    private double GetOverlayWidth()
    {
        if (HardwareOverlay.ActualWidth > 0)
        {
            return HardwareOverlay.ActualWidth;
        }

        return double.IsNaN(HardwareOverlay.Width) ? 0 : HardwareOverlay.Width;
    }

    private double GetOverlayHeight()
    {
        if (HardwareOverlay.ActualHeight > 0)
        {
            return HardwareOverlay.ActualHeight;
        }

        return double.IsNaN(HardwareOverlay.Height) ? 0 : HardwareOverlay.Height;
    }

    private static double ClampHardwareOverlayCoordinate(double value, double viewportLength, double overlayLength)
    {
        double maximum = Math.Max(0, viewportLength - Math.Max(0, overlayLength));
        return Math.Clamp(value, 0, maximum);
    }

    private static TextBlock CreateHardwareOverlayText(string text, double fontSize)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = fontSize,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            TextWrapping = TextWrapping.NoWrap,
        };
    }

    private static StackPanel CreateHardwareMetricRow(HardwareOverlayMetric metric, double fontSize)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(HardwareOverlayIconFactory.CreateIcon(metric.IconKind, Math.Max(17, fontSize + 2)));
        row.Children.Add(CreateHardwareOverlayText(metric.ValueText, fontSize));
        return row;
    }

    private static UIElement CreateHardwareOverlayElement(HardwareOverlayElementState element, double fallbackFontSize)
    {
        if (element.Kind == HardwareOverlayElementKind.Image && TryCreateBitmapImage(element.ImagePath, out BitmapImage? bitmap))
        {
            return new Microsoft.UI.Xaml.Controls.Image
            {
                Source = bitmap,
                Width = element.Width,
                Height = element.Height,
                Stretch = Stretch.UniformToFill,
                Opacity = element.Opacity,
            };
        }

        if (element.Kind == HardwareOverlayElementKind.Sensor)
        {
            return CreateHardwareOverlaySensorElement(element, fallbackFontSize);
        }

        return new TextBlock
        {
            Text = element.Text,
            FontFamily = new FontFamily(element.FontFamily),
            FontSize = Math.Max(8, element.FontSize > 0 ? element.FontSize : fallbackFontSize),
            Foreground = CreateElementBrush(element.Foreground),
            Width = element.Width,
            Height = element.Height,
            TextWrapping = TextWrapping.WrapWholeWords,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = element.Opacity,
        };
    }

    private static UIElement CreateHardwareOverlaySensorElement(HardwareOverlayElementState element, double fallbackFontSize)
    {
        double fontSize = Math.Max(8, element.FontSize > 0 ? element.FontSize : fallbackFontSize);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Width = element.Width,
            Height = element.Height,
            Opacity = element.Opacity,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Brush brush = CreateElementBrush(element.Foreground);
        row.Children.Add(HardwareOverlayIconFactory.CreateIcon(element.IconKind, Math.Max(17, fontSize + 2), brush));
        row.Children.Add(new TextBlock
        {
            Text = element.Text,
            FontFamily = new FontFamily(element.FontFamily),
            FontSize = fontSize,
            Foreground = brush,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    private static bool TryCreateBitmapImage(string path, out BitmapImage? bitmap)
    {
        bitmap = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            bitmap = new BitmapImage(new Uri(path));
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            bitmap = null;
            return false;
        }
    }

    private static SolidColorBrush CreateElementBrush(string value)
    {
        if (TryParseColor(value, out global::Windows.UI.Color color))
        {
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(Microsoft.UI.Colors.White);
    }

    private static bool TryParseColor(string value, out global::Windows.UI.Color color)
    {
        color = Microsoft.UI.Colors.White;
        string hex = value.Trim().TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (hex.Length != 8 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint argb))
        {
            return false;
        }

        color = Microsoft.UI.ColorHelper.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
        return true;
    }

    public async Task ShowImageAsync(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        HideError();
        StopVideo();
        var bitmap = new BitmapImage();
        Task bitmapOpened = AttachBitmapLoadHandlers(bitmap);
        NextImage.Source = bitmap;
        bitmap.UriSource = new Uri(path);
        await bitmapOpened;
        PrepareNextImageForTransition();

        bool hasCurrentImage = CurrentImage.Source is not null;
        if (!WallpaperTransitionPolicy.ShouldAnimate(_profile.Transition, _profile.TransitionDurationMs))
        {
            CommitImage(bitmap, path);
            return;
        }

        if (_profile.Transition == WallpaperTransition.Slide)
        {
            double nextToX = _nextTransform.X;
            if (hasCurrentImage)
            {
                await AnimateSlideAsync(_currentTransform.X, nextToX);
            }
            else
            {
                await AnimateInitialSlideAsync(nextToX);
            }
        }
        else
        {
            if (hasCurrentImage)
            {
                await AnimateFadeAsync();
            }
            else
            {
                await AnimateInitialFadeAsync();
            }
        }

        CommitImage(bitmap, path);
    }

    public async Task ShowVideoAsync(string path, bool loop)
    {
        if (!File.Exists(path))
        {
            ShowVideoError(path, LocalizedStrings.Get("VideoPlaybackFileMissing"));
            return;
        }

        if (FileLinkResolver.TryGetMissingLinkTarget(path, out string missingTarget))
        {
            ShowVideoError(path, LocalizedStrings.Format("VideoPlaybackMissingLinkTargetFormat", missingTarget));
            return;
        }

        int requestVersion = BeginMediaRequest();
        MediaPlayer player = ReplaceMediaPlayer(loop);
        ClearImageSources();
        HideError();
        _currentKind = MediaKind.Video;
        _currentImagePath = path;
        VideoPlayer.Visibility = Visibility.Collapsed;
        try
        {
            string playbackPath = FileLinkResolver.GetFinalPath(path);
            StorageFile file = await StorageFile.GetFileFromPathAsync(playbackPath);
            if (!IsCurrentMediaRequest(requestVersion) || !ReferenceEquals(player, _mediaPlayer))
            {
                ResetMediaPlayerSource(player);
                return;
            }

            player.Source = MediaSource.CreateFromStorageFile(file);
            VideoPlayer.Visibility = Visibility.Visible;
            ApplyProfile(_profile);
            _isShowingWallpaper = true;
            player.Play();
            if (_videoPausedByCoverage)
            {
                player.Pause();
            }
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            if (IsCurrentMediaRequest(requestVersion))
            {
                ShowVideoError(path, exception.Message);
            }
        }
    }

    public void SetVideoPausedByCoverage(bool isPaused)
    {
        if (_videoPausedByCoverage == isPaused)
        {
            return;
        }

        _videoPausedByCoverage = isPaused;
        if (_currentKind != MediaKind.Video || VideoPlayer.Visibility != Visibility.Visible)
        {
            return;
        }

        if (isPaused)
        {
            _mediaPlayer.Pause();
        }
        else
        {
            _mediaPlayer.Play();
        }
    }

    public async Task UpdateProfileWithTransitionAsync(MonitorProfile profile)
    {
        if (CurrentImage.Source is null || string.IsNullOrWhiteSpace(_currentImagePath))
        {
            ApplyProfile(profile);
            return;
        }

        double previousOffsetX = _currentTransform.X;
        _profile = profile;
        NextImage.Source = CurrentImage.Source;
        ApplyImageProfile(NextImage, _nextTransform, profile);

        if (!WallpaperTransitionPolicy.ShouldAnimate(profile.Transition, profile.TransitionDurationMs))
        {
            ApplyProfile(profile);
            CurrentImage.Opacity = 1;
            NextImage.Opacity = 0;
            NextImage.Source = null;
            return;
        }

        if (profile.Transition == WallpaperTransition.Slide)
        {
            await AnimateSlideAsync(previousOffsetX, _nextTransform.X);
        }
        else
        {
            await AnimateFadeAsync();
        }

        ApplyProfile(profile);
        CurrentImage.Opacity = 1;
        NextImage.Opacity = 0;
        NextImage.Source = null;
    }

    private void ConfigureWindow()
    {
        Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        NativeMethods.RemoveWindowFrame(_hwnd);
    }

    private void ApplyMute(MonitorProfile profile)
    {
        _mediaPlayer.IsMuted = _forceMuted || !profile.VideoSoundEnabled;
    }

    private Task AnimateFadeAsync()
    {
        var storyboard = new Storyboard();
        storyboard.Children.Add(CreateOpacityAnimation(CurrentImage, 1, 0));
        storyboard.Children.Add(CreateOpacityAnimation(NextImage, 0, 1));
        return BeginStoryboardAsync(storyboard);
    }

    private Task AnimateInitialFadeAsync()
    {
        CurrentImage.Opacity = 0;
        NextImage.Opacity = 0;

        var storyboard = new Storyboard();
        storyboard.Children.Add(CreateOpacityAnimation(NextImage, 0, 1));
        return BeginStoryboardAsync(storyboard);
    }

    private Task AnimateSlideAsync(double currentFromX, double nextToX)
    {
        NextImage.Opacity = 1;
        _nextTransform.X = ActualWidthOrFallback();

        var storyboard = new Storyboard();
        storyboard.Children.Add(CreateTranslateAnimation(_currentTransform, currentFromX, -ActualWidthOrFallback()));
        storyboard.Children.Add(CreateTranslateAnimation(_nextTransform, ActualWidthOrFallback(), nextToX));
        return BeginStoryboardAsync(storyboard);
    }

    private Task AnimateInitialSlideAsync(double nextToX)
    {
        NextImage.Opacity = 1;
        _nextTransform.X = ActualWidthOrFallback();

        var storyboard = new Storyboard();
        storyboard.Children.Add(CreateTranslateAnimation(_nextTransform, ActualWidthOrFallback(), nextToX));
        return BeginStoryboardAsync(storyboard);
    }

    private void CommitImage(BitmapImage bitmap, string path)
    {
        _currentKind = MediaKind.Image;
        CurrentImage.Source = bitmap;
        _currentImagePath = path;
        _isShowingWallpaper = true;
        CurrentImage.Opacity = 1;
        ApplyImageLayout(CurrentImage, _currentTransform, _profile);
        NextImage.Opacity = 0;
        NextImage.Source = null;
        ApplyImageLayout(NextImage, _nextTransform, _profile);
    }

    private void PrepareNextImageForTransition()
    {
        NextImage.Stretch = Stretch.Fill;
        ApplyImageLayout(NextImage, _nextTransform, _profile);
        _mediaPlayer.IsLoopingEnabled = _profile.VideoLoop;
        ApplyMute(_profile);
    }

    private void ClearImageSources()
    {
        CurrentImage.Source = null;
        NextImage.Source = null;
    }

    private void StopVideo()
    {
        CancelMediaRequest();
        ResetMediaPlayerSource(_mediaPlayer);
        VideoPlayer.Visibility = Visibility.Collapsed;
        _currentKind = MediaKind.Image;
    }

    private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
    {
        if (!ReferenceEquals(sender, _mediaPlayer))
        {
            return;
        }

        int requestVersion = _mediaRequestVersion;
        DispatcherQueue.TryEnqueue(() => NotifyVideoEnded(requestVersion));
    }

    private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        if (!ReferenceEquals(sender, _mediaPlayer))
        {
            return;
        }

        int requestVersion = _mediaRequestVersion;
        string path = _currentImagePath;
        string errorMessage = string.IsNullOrWhiteSpace(args.ErrorMessage)
            ? args.Error.ToString()
            : $"{args.Error}: {args.ErrorMessage}";
        AppLog.Write($"Media failed: {errorMessage}");
        DispatcherQueue.TryEnqueue(() =>
        {
            if (IsCurrentMediaRequest(requestVersion) && _currentKind == MediaKind.Video && _currentImagePath == path)
            {
                ShowVideoError(path, errorMessage);
            }
        });
    }

    private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
    {
        if (!ReferenceEquals(sender, _mediaPlayer))
        {
            return;
        }

        int requestVersion = _mediaRequestVersion;
        string path = _currentImagePath;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (IsCurrentMediaRequest(requestVersion) && _currentKind == MediaKind.Video && _currentImagePath == path)
            {
                HideError();
                ApplyVideoLayout(_profile);
            }
        });
    }

    private void ShowVideoError(string path, string message)
    {
        CancelMediaRequest();
        ResetMediaPlayerSource(_mediaPlayer);
        VideoPlayer.Visibility = Visibility.Collapsed;
        ErrorTitleText.Text = LocalizedStrings.Get("VideoPlaybackErrorTitle");
        ErrorDetailText.Text = LocalizedStrings.Format("VideoPlaybackErrorFormat", System.IO.Path.GetFileName(path), message);
        ErrorOverlay.Visibility = Visibility.Visible;
    }

    private void HideError()
    {
        ErrorOverlay.Visibility = Visibility.Collapsed;
        ErrorTitleText.Text = string.Empty;
        ErrorDetailText.Text = string.Empty;
    }

    private void NotifyVideoEnded(int requestVersion)
    {
        if (!IsCurrentMediaRequest(requestVersion) || _currentKind != MediaKind.Video || VideoPlaybackPolicy.ShouldLoopVideo(_profile))
        {
            return;
        }

        VideoEnded?.Invoke(this, EventArgs.Empty);
    }

    private static void ApplyImageProfile(Microsoft.UI.Xaml.Controls.Image image, TranslateTransform transform, MonitorProfile profile)
    {
        image.Stretch = Stretch.Fill;
        ApplyImageLayout(image, transform, profile);
    }

    private static void ApplyImageLayout(Microsoft.UI.Xaml.Controls.Image image, TranslateTransform transform, MonitorProfile profile)
    {
        if (image.Source is not BitmapImage bitmap)
        {
            return;
        }

        WallpaperElementLayout layout = WallpaperLayoutCalculator.Calculate(
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            GetViewportWidth(image),
            GetViewportHeight(image),
            profile.ScaleMode,
            profile.OffsetX,
            profile.OffsetY);
        image.Width = layout.Width;
        image.Height = layout.Height;
        Canvas.SetLeft(image, layout.OffsetX);
        Canvas.SetTop(image, layout.OffsetY);
        transform.X = 0;
        transform.Y = 0;
    }

    private void ApplyVideoLayout(MonitorProfile profile)
    {
        if (_currentKind != MediaKind.Video || VideoPlayer.Visibility != Visibility.Visible)
        {
            return;
        }

        double sourceWidth = _mediaPlayer.PlaybackSession.NaturalVideoWidth;
        double sourceHeight = _mediaPlayer.PlaybackSession.NaturalVideoHeight;
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            return;
        }

        WallpaperElementLayout layout = WallpaperLayoutCalculator.Calculate(
            sourceWidth,
            sourceHeight,
            GetViewportWidth(Root),
            GetViewportHeight(Root),
            profile.ScaleMode,
            profile.OffsetX,
            profile.OffsetY);
        VideoPlayer.Width = layout.Width;
        VideoPlayer.Height = layout.Height;
        Canvas.SetLeft(VideoPlayer, layout.OffsetX);
        Canvas.SetTop(VideoPlayer, layout.OffsetY);
        _videoTransform.X = 0;
        _videoTransform.Y = 0;
    }

    private int BeginMediaRequest()
    {
        return ++_mediaRequestVersion;
    }

    private void CancelMediaRequest()
    {
        _mediaRequestVersion++;
    }

    private bool IsCurrentMediaRequest(int requestVersion)
    {
        return !_isClosed && requestVersion == _mediaRequestVersion;
    }

    private MediaPlayer ReplaceMediaPlayer(bool loop)
    {
        MediaPlayer previousPlayer = _mediaPlayer;
        DetachMediaPlayerEvents(previousPlayer);
        ResetMediaPlayerSource(previousPlayer);

        var nextPlayer = CreateMediaPlayer(loop);
        _mediaPlayer = nextPlayer;
        VideoPlayer.SetMediaPlayer(nextPlayer);
        DisposeMediaPlayer(previousPlayer);
        return nextPlayer;
    }

    private MediaPlayer CreateMediaPlayer(bool loop)
    {
        var player = new MediaPlayer
        {
            IsLoopingEnabled = loop,
            IsMuted = _forceMuted || !_profile.VideoSoundEnabled,
        };
        player.MediaEnded += MediaPlayer_MediaEnded;
        player.MediaFailed += MediaPlayer_MediaFailed;
        player.MediaOpened += MediaPlayer_MediaOpened;
        return player;
    }

    private void DetachMediaPlayerEvents(MediaPlayer player)
    {
        player.MediaEnded -= MediaPlayer_MediaEnded;
        player.MediaFailed -= MediaPlayer_MediaFailed;
        player.MediaOpened -= MediaPlayer_MediaOpened;
    }

    private static void ResetMediaPlayerSource(MediaPlayer player)
    {
        try
        {
            player.Pause();
            player.Source = null;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private void DisposeMediaPlayer(MediaPlayer player)
    {
        DetachMediaPlayerEvents(player);
        ResetMediaPlayerSource(player);
        player.Dispose();
    }

    private DoubleAnimation CreateOpacityAnimation(UIElement target, double from, double to)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(_profile.TransitionDurationMs),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        return animation;
    }

    private DoubleAnimation CreateTranslateAnimation(TranslateTransform target, double from, double to)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(_profile.TransitionDurationMs),
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "X");
        return animation;
    }

    private static Task BeginStoryboardAsync(Storyboard storyboard)
    {
        var completion = new TaskCompletionSource();
        storyboard.Completed += (_, _) => completion.TrySetResult();
        storyboard.Begin();
        return completion.Task;
    }

    private double ActualWidthOrFallback()
    {
        return Root.ActualWidth > 0 ? Root.ActualWidth : 800;
    }

    private double ActualHeightOrFallback()
    {
        return Root.ActualHeight > 0 ? Root.ActualHeight : 450;
    }

    private static double GetViewportWidth(FrameworkElement element)
    {
        double width = element.XamlRoot?.Size.Width ?? 0;
        if (width > 0)
        {
            return width;
        }

        return element.ActualWidth > 0 ? element.ActualWidth : 800;
    }

    private static double GetViewportHeight(FrameworkElement element)
    {
        double height = element.XamlRoot?.Size.Height ?? 0;
        if (height > 0)
        {
            return height;
        }

        return element.ActualHeight > 0 ? element.ActualHeight : 450;
    }

    private static Task AttachBitmapLoadHandlers(BitmapImage bitmap)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bitmap.ImageOpened += (_, _) => completion.TrySetResult();
        bitmap.ImageFailed += (_, args) => completion.TrySetException(new InvalidOperationException(args.ErrorMessage));
        return completion.Task;
    }
}
