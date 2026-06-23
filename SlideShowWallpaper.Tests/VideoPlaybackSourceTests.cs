using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class VideoPlaybackSourceTests
{
    [Fact]
    public void OpenSequentialReadStream_WithExistingFile_OpensReadableStream()
    {
        string directory = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "clip.mp4");
        File.WriteAllBytes(path, [1, 2, 3, 4]);

        try
        {
            using FileStream stream = VideoPlaybackSource.OpenSequentialReadStream(path);

            Assert.True(stream.CanRead);
            Assert.Equal(1, stream.ReadByte());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void OpenSequentialReadStream_UsesSequentialScanFileOption()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "VideoPlaybackSource.cs"));

        Assert.Contains("FileOptions.Asynchronous | FileOptions.SequentialScan", source);
        Assert.Contains("MediaSource.CreateFromStream", source);
    }

    [Theory]
    [InlineData("clip.mp4", "video/mp4")]
    [InlineData("clip.mkv", "video/x-matroska")]
    [InlineData("clip.webm", "video/webm")]
    public void GetContentType_WithSupportedVideoExtension_ReturnsVideoMimeType(string fileName, string expected)
    {
        Assert.Equal(expected, VideoPlaybackSource.GetContentType(fileName));
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SlideShowWallpaper.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Project root not found.");
    }
}
