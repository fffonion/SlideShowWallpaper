using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using WinRT.Interop;

namespace SlideShowWallpaper.Windows;

public sealed partial class WallpaperWindow : Window
{
    private readonly TranslateTransform _currentTransform = new();
    private readonly TranslateTransform _nextTransform = new();
    private MonitorProfile _profile;
    private string _currentImagePath = string.Empty;

    public WallpaperWindow(MonitorProfile profile)
    {
        _profile = profile;
        InitializeComponent();
        SystemBackdrop = null;
        CurrentImage.RenderTransform = _currentTransform;
        NextImage.RenderTransform = _nextTransform;
        ConfigureWindow();
        ApplyProfile(profile);
    }

    public void ApplyProfile(MonitorProfile profile)
    {
        _profile = profile;
        Stretch stretch = WallpaperScaleModeMapper.ToImageStretch(profile.ScaleMode);

        CurrentImage.Stretch = stretch;
        NextImage.Stretch = stretch;
        _currentTransform.X = profile.OffsetX;
        _currentTransform.Y = profile.OffsetY;
        _nextTransform.X = profile.OffsetX;
        _nextTransform.Y = profile.OffsetY;
    }

    public async Task ShowImageAsync(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

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
        CurrentImage.Source = bitmap;
        _currentImagePath = path;
        CurrentImage.Opacity = 1;
        _currentTransform.X = _profile.OffsetX;
        _currentTransform.Y = _profile.OffsetY;
        NextImage.Opacity = 0;
        _nextTransform.X = _profile.OffsetX;
        _nextTransform.Y = _profile.OffsetY;
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
