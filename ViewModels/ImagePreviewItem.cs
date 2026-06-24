using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
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
    private DispatcherQueue? _dispatcherQueue;
    private ImageSource? _thumbnail;
    private CancellationTokenSource? _thumbnailCancellation;
    private int _thumbnailLoadVersion;
    private bool _thumbnailLoadScheduled;
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

    public Visibility ImageVisibility => !_thumbnailFailed && (!RequiresThumbnailPlaceholder || _thumbnailLoaded) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingVisibility => IsThumbnailLoading ? Visibility.Visible : Visibility.Collapsed;

    public bool IsThumbnailLoading => RequiresThumbnailPlaceholder && _thumbnailLoadStarted && !_thumbnailLoaded && !_thumbnailFailed;

    public Visibility PlaceholderVisibility => _thumbnailFailed || (Metadata.Kind == MediaKind.Video && !_thumbnailLoaded && !IsThumbnailLoading) ? Visibility.Visible : Visibility.Collapsed;

    private bool RequiresThumbnailPlaceholder => Metadata.Kind == MediaKind.Video || string.Equals(System.IO.Path.GetExtension(Metadata.Path), ".ndf", StringComparison.OrdinalIgnoreCase);

    public ImageSource? Thumbnail
    {
        get
        {
            if (_thumbnail is null && !_thumbnailLoaded && File.Exists(Path))
            {
                QueueThumbnailLoad();
            }

            return _thumbnail;
        }
    }

    public void ClearThumbnail()
    {
        RunOnUiThread(() =>
        {
            _thumbnailLoadVersion++;
            _thumbnailCancellation?.Cancel();
            _thumbnailCancellation = null;
            _thumbnailLoadScheduled = false;
            _thumbnailLoadStarted = false;
            _thumbnailLoaded = false;
            _thumbnailFailed = false;
            _thumbnail = null;
            NotifyThumbnailStateChanged(thumbnailChanged: true);
        });
    }

    private void QueueThumbnailLoad()
    {
        _dispatcherQueue ??= TryGetDispatcherQueue();
        if (_thumbnailLoadStarted || _thumbnailLoadScheduled)
        {
            return;
        }

        _thumbnailLoadScheduled = true;
        int scheduledVersion = _thumbnailLoadVersion;
        if (_dispatcherQueue is { } dispatcherQueue)
        {
            if (dispatcherQueue.TryEnqueue(async () => await StartThumbnailLoadAsync(scheduledVersion)))
            {
                return;
            }
        }

        _ = Task.Run(async () => await StartThumbnailLoadAsync(scheduledVersion));
    }

    private async Task StartThumbnailLoadAsync(int scheduledVersion)
    {
        if (scheduledVersion != _thumbnailLoadVersion || !_thumbnailLoadScheduled || _thumbnailLoadStarted)
        {
            return;
        }

        _thumbnailLoadScheduled = false;
        _thumbnailLoadStarted = true;
        int version = ++_thumbnailLoadVersion;
        _thumbnailCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _thumbnailCancellation = cancellation;
        NotifyThumbnailStateChanged(thumbnailChanged: false);
        try
        {
            string thumbnailPath = await _thumbnailLoader(Metadata, cancellation.Token);
            RunOnUiThread(() =>
            {
                if (version != _thumbnailLoadVersion)
                {
                    return;
                }

                _thumbnail = _thumbnailFactory(thumbnailPath);
                _thumbnailLoaded = true;
                _thumbnailFailed = false;
                NotifyThumbnailStateChanged(thumbnailChanged: true);
            });
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            RunOnUiThread(() =>
            {
                if (version != _thumbnailLoadVersion)
                {
                    return;
                }

                _thumbnailFailed = true;
                _thumbnailLoaded = false;
                _thumbnail = null;
                NotifyThumbnailStateChanged(thumbnailChanged: true);
            });
        }
        finally
        {
            RunOnUiThread(() =>
            {
                if (ReferenceEquals(_thumbnailCancellation, cancellation))
                {
                    _thumbnailCancellation = null;
                }
            });

            cancellation.Dispose();
        }
    }

    private void NotifyThumbnailStateChanged(bool thumbnailChanged)
    {
        if (thumbnailChanged)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImageVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoadingVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsThumbnailLoading)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlaceholderVisibility)));
    }

    private void RunOnUiThread(Action action)
    {
        if (_dispatcherQueue is not { } dispatcherQueue || dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        dispatcherQueue.TryEnqueue(() => action());
    }

    private static DispatcherQueue? TryGetDispatcherQueue()
    {
        try
        {
            return DispatcherQueue.GetForCurrentThread();
        }
        catch (COMException)
        {
            return null;
        }
    }
}
