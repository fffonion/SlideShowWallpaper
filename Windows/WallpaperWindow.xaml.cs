using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Controls;
using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
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
    private MediaPlayer _mediaPlayer;
    private MonitorProfile _profile;
    private string _currentImagePath = string.Empty;
    private MediaKind _currentKind = MediaKind.Image;
    private int _mediaRequestVersion;
    private bool _isClosed;
    private bool _videoPausedByCoverage;
    private bool _forceMuted;

    public WallpaperWindow(MonitorProfile profile)
    {
        _profile = profile;
        InitializeComponent();
        Title = LocalizedStrings.Get("PlayerTitle");
        SystemBackdrop = null;
        CurrentImage.RenderTransform = _currentTransform;
        NextImage.RenderTransform = _nextTransform;
        VideoPlayer.RenderTransform = _videoTransform;
        _mediaPlayer = CreateMediaPlayer(profile.VideoLoop);
        VideoPlayer.SetMediaPlayer(_mediaPlayer);
        ConfigureWindow();
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
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        NativeMethods.RemoveWindowFrame(hwnd);
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
        ErrorDetailText.Text = LocalizedStrings.Format("VideoPlaybackErrorFormat", Path.GetFileName(path), message);
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
