using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using Windows.Media.Core;
using Windows.Media.Playback;
using WinRT.Interop;

namespace SlideShowWallpaper.Windows;

public sealed partial class WallpaperWindow : Window
{
    private readonly TranslateTransform _currentTransform = new();
    private readonly TranslateTransform _nextTransform = new();
    private readonly TranslateTransform _videoTransform = new();
    private readonly MediaPlayer _mediaPlayer = new();
    private MonitorProfile _profile;
    private string _currentImagePath = string.Empty;
    private MediaKind _currentKind = MediaKind.Image;
    private bool _videoPausedByCoverage;

    public WallpaperWindow(MonitorProfile profile)
    {
        _profile = profile;
        InitializeComponent();
        Title = LocalizedStrings.Get("PlayerTitle");
        SystemBackdrop = null;
        CurrentImage.RenderTransform = _currentTransform;
        NextImage.RenderTransform = _nextTransform;
        VideoPlayer.RenderTransform = _videoTransform;
        _mediaPlayer.IsMuted = true;
        _mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
        _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
        VideoPlayer.SetMediaPlayer(_mediaPlayer);
        ConfigureWindow();
        ApplyProfile(profile);
        Closed += (_, _) =>
        {
            StopVideo();
            ClearImageSources();
            _mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
            _mediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
            _mediaPlayer.Dispose();
        };
    }

    public event EventHandler? VideoEnded;

    public void ApplyProfile(MonitorProfile profile)
    {
        _profile = profile;
        Stretch stretch = WallpaperScaleModeMapper.ToImageStretch(profile.ScaleMode);

        CurrentImage.Stretch = stretch;
        NextImage.Stretch = stretch;
        VideoPlayer.Stretch = stretch;
        _mediaPlayer.IsLoopingEnabled = profile.VideoLoop;
        _mediaPlayer.IsMuted = !profile.VideoSoundEnabled;
        _currentTransform.X = profile.OffsetX;
        _currentTransform.Y = profile.OffsetY;
        _nextTransform.X = profile.OffsetX;
        _nextTransform.Y = profile.OffsetY;
        _videoTransform.X = profile.OffsetX;
        _videoTransform.Y = profile.OffsetY;
    }

    public async Task ShowImageAsync(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        StopVideo();
        var bitmap = new BitmapImage(new Uri(path));
        NextImage.Source = bitmap;
        ApplyProfile(_profile);

        bool hasCurrentImage = CurrentImage.Source is not null;
        if (!WallpaperTransitionPolicy.ShouldAnimate(_profile.Transition, _profile.TransitionDurationMs))
        {
            CommitImage(bitmap, path);
            return;
        }

        if (_profile.Transition == WallpaperTransition.Slide)
        {
            if (hasCurrentImage)
            {
                await AnimateSlideAsync(_profile.OffsetX, _profile.OffsetX);
            }
            else
            {
                await AnimateInitialSlideAsync();
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

    public Task ShowVideoAsync(string path, bool loop)
    {
        if (!File.Exists(path))
        {
            return Task.CompletedTask;
        }

        ClearImageSources();
        _currentKind = MediaKind.Video;
        _currentImagePath = path;
        VideoPlayer.Visibility = Visibility.Visible;
        _mediaPlayer.IsLoopingEnabled = loop;
        _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(path));
        ApplyProfile(_profile);
        _mediaPlayer.Play();
        if (_videoPausedByCoverage)
        {
            _mediaPlayer.Pause();
        }

        return Task.CompletedTask;
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

        double previousOffsetX = _profile.OffsetX;
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
            await AnimateSlideAsync(previousOffsetX, profile.OffsetX);
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

    private Task AnimateInitialSlideAsync()
    {
        NextImage.Opacity = 1;
        _nextTransform.X = ActualWidthOrFallback();

        var storyboard = new Storyboard();
        storyboard.Children.Add(CreateTranslateAnimation(_nextTransform, ActualWidthOrFallback(), _profile.OffsetX));
        return BeginStoryboardAsync(storyboard);
    }

    private void CommitImage(BitmapImage bitmap, string path)
    {
        _currentKind = MediaKind.Image;
        CurrentImage.Source = bitmap;
        _currentImagePath = path;
        CurrentImage.Opacity = 1;
        _currentTransform.X = _profile.OffsetX;
        _currentTransform.Y = _profile.OffsetY;
        NextImage.Opacity = 0;
        NextImage.Source = null;
        _nextTransform.X = _profile.OffsetX;
        _nextTransform.Y = _profile.OffsetY;
    }

    private void ClearImageSources()
    {
        CurrentImage.Source = null;
        NextImage.Source = null;
    }

    private void StopVideo()
    {
        if (VideoPlayer.Visibility == Visibility.Visible)
        {
            _mediaPlayer.Pause();
        }

        _mediaPlayer.Source = null;
        VideoPlayer.Visibility = Visibility.Collapsed;
        _currentKind = MediaKind.Image;
    }

    private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
    {
        NotifyVideoEnded();
    }

    private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        NotifyVideoEnded();
    }

    private void NotifyVideoEnded()
    {
        if (_currentKind != MediaKind.Video || _profile.VideoLoop)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() => VideoEnded?.Invoke(this, EventArgs.Empty));
    }

    private static void ApplyImageProfile(Microsoft.UI.Xaml.Controls.Image image, TranslateTransform transform, MonitorProfile profile)
    {
        image.Stretch = WallpaperScaleModeMapper.ToImageStretch(profile.ScaleMode);
        transform.X = profile.OffsetX;
        transform.Y = profile.OffsetY;
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
}
