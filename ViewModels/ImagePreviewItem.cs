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
    private readonly Func<string, ImageSource?> _thumbnailFactory;
    private ImageSource? _thumbnail;
    private CancellationTokenSource? _thumbnailCancellation;
    private int _thumbnailLoadVersion;
    private bool _thumbnailLoadStarted;
    private bool _thumbnailLoaded;
    private bool _thumbnailFailed;

    public ImagePreviewItem(ImageMetadata metadata)
        : this(metadata, ThumbnailCache.GetOrCreateThumbnailAsync)
    {
    }

    public ImagePreviewItem(ImageMetadata metadata, Func<ImageMetadata, CancellationToken, Task<string>> thumbnailLoader)
        : this(metadata, thumbnailLoader, path => new BitmapImage(new Uri(path)))
    {
    }

    internal ImagePreviewItem(ImageMetadata metadata, Func<ImageMetadata, CancellationToken, Task<string>> thumbnailLoader, Func<string, ImageSource?> thumbnailFactory)
    {
        Metadata = metadata;
        _thumbnailLoader = thumbnailLoader;
        _thumbnailFactory = thumbnailFactory;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageMetadata Metadata { get; }

    public string Path => Metadata.Path;

    public string FileName => Metadata.FileName;

    public string Details => $"{Metadata.ModifiedUtc.ToLocalTime():g}";

    public Visibility ImageVisibility => !_thumbnailFailed && (Metadata.Kind == MediaKind.Image || _thumbnailLoaded) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PlaceholderVisibility => _thumbnailFailed || (Metadata.Kind == MediaKind.Video && !_thumbnailLoaded) ? Visibility.Visible : Visibility.Collapsed;

    public ImageSource? Thumbnail
    {
        get
        {
            if (_thumbnail is null && !_thumbnailLoaded && File.Exists(Path))
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
        _thumbnailLoaded = false;
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

            _thumbnail = _thumbnailFactory(thumbnailPath);
            _thumbnailLoaded = true;
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
            _thumbnailLoaded = false;
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
