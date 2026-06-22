using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.ViewModels;

public sealed class ImagePreviewItem : INotifyPropertyChanged
{
    private static readonly ThumbnailCacheService ThumbnailCache = new();
    private ImageSource? _thumbnail;
    private CancellationTokenSource? _thumbnailCancellation;
    private int _thumbnailLoadVersion;
    private bool _thumbnailLoadStarted;

    public ImagePreviewItem(ImageMetadata metadata)
    {
        Metadata = metadata;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ImageMetadata Metadata { get; }

    public string Path => Metadata.Path;

    public string FileName => Metadata.FileName;

    public string Details => $"{Metadata.ModifiedUtc.ToLocalTime():g}";

    public ImageSource? Thumbnail
    {
        get
        {
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
        _thumbnail = null;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
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
            string thumbnailPath = await ThumbnailCache.GetOrCreateThumbnailAsync(Metadata, cancellation.Token);
            if (version != _thumbnailLoadVersion)
            {
                return;
            }

            _thumbnail = new BitmapImage(new Uri(thumbnailPath));
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

            _thumbnail = new BitmapImage
            {
                DecodePixelWidth = 128,
                UriSource = new Uri(Path),
            };
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
    }
}
