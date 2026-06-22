using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.ViewModels;

public sealed class ImagePreviewItem : INotifyPropertyChanged
{
    private static readonly ThumbnailCacheService ThumbnailCache = new();
    private readonly Func<ImageMetadata, CancellationToken, Task<string>> _thumbnailLoader;
    private ImageSource? _thumbnail;
    private CancellationTokenSource? _thumbnailCancellation;
    private int _thumbnailLoadVersion;
    private bool _thumbnailLoadStarted;
    private bool _thumbnailFailed;

    public ImagePreviewItem(ImageMetadata metadata)
        : this(metadata, ThumbnailCache.GetOrCreateThumbnailAsync)
    {
    }

    public ImagePreviewItem(ImageMetadata metadata, Func<ImageMetadata, CancellationToken, Task<string>> thumbnailLoader)
    {
        Metadata = metadata;
        _thumbnailLoader = thumbnailLoader;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageMetadata Metadata { get; }

    public string Path => Metadata.Path;

    public string FileName => Metadata.FileName;

    public string Details => $"{Metadata.ModifiedUtc.ToLocalTime():g}";

    public Visibility ImageVisibility => Metadata.Kind == MediaKind.Image && !_thumbnailFailed ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PlaceholderVisibility => Metadata.Kind == MediaKind.Video || _thumbnailFailed ? Visibility.Visible : Visibility.Collapsed;

    public ImageSource? Thumbnail
    {
        get
        {
            if (Metadata.Kind == MediaKind.Video)
            {
                return null;
            }

            if (_thumbnail is null && File.Exists(Path))
            {
                StartThumbnailLoad();
            }

            return _thumbnail;
        }
    }

    public void ClearThumbnail()
    {
        _thumbnailLoadVersion++;
        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation = null;
        _thumbnailLoadStarted = false;
        _thumbnailFailed = false;
        _thumbnail = null;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaceholderVisibility)));
    }

    private async void StartThumbnailLoad()
    {
        if (_thumbnailLoadStarted)
        {
            return;
        }

        _thumbnailLoadStarted = true;
        int version = ++_thumbnailLoadVersion;
        _thumbnailCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _thumbnailCancellation = cancellation;
        try
        {
            string thumbnailPath = await _thumbnailLoader(Metadata, cancellation.Token);
            if (version != _thumbnailLoadVersion)
            {
                return;
            }

            _thumbnail = new BitmapImage(new Uri(thumbnailPath));
            _thumbnailFailed = false;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            if (version != _thumbnailLoadVersion)
            {
                return;
            }

            _thumbnailFailed = true;
            _thumbnail = null;
        }
        finally
        {
            if (ReferenceEquals(_thumbnailCancellation, cancellation))
            {
                _thumbnailCancellation = null;
            }

            cancellation.Dispose();
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaceholderVisibility)));
    }
}
