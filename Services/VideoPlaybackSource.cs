using Windows.Media.Core;
using Windows.Storage.Streams;

namespace SlideShowWallpaper.Services;

public sealed class VideoPlaybackSource : IDisposable
{
    private const int BufferSize = 1024 * 1024;

    private readonly FileStream _fileStream;
    private readonly IRandomAccessStream _randomAccessStream;

    private VideoPlaybackSource(FileStream fileStream, IRandomAccessStream randomAccessStream, MediaSource mediaSource)
    {
        _fileStream = fileStream;
        _randomAccessStream = randomAccessStream;
        MediaSource = mediaSource;
    }

    public MediaSource MediaSource { get; }

    public static VideoPlaybackSource Open(string path)
    {
        string finalPath = FileLinkResolver.GetFinalPath(path);
        FileStream fileStream = OpenSequentialReadStream(finalPath);
        try
        {
            IRandomAccessStream randomAccessStream = fileStream.AsRandomAccessStream();
            try
            {
                MediaSource mediaSource = MediaSource.CreateFromStream(randomAccessStream, GetContentType(finalPath));
                return new VideoPlaybackSource(fileStream, randomAccessStream, mediaSource);
            }
            catch
            {
                randomAccessStream.Dispose();
                throw;
            }
        }
        catch
        {
            fileStream.Dispose();
            throw;
        }
    }

    public static FileStream OpenSequentialReadStream(string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    public static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".avi" => "video/x-msvideo",
            ".m4v" => "video/x-m4v",
            ".mkv" => "video/x-matroska",
            ".mov" => "video/quicktime",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".wmv" => "video/x-ms-wmv",
            _ => "video/mp4",
        };
    }

    public void Dispose()
    {
        MediaSource.Dispose();
        _randomAccessStream.Dispose();
        _fileStream.Dispose();
    }
}
