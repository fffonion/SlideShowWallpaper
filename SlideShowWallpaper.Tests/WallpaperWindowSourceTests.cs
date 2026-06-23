namespace SlideShowWallpaper.Tests;

public sealed class WallpaperWindowSourceTests
{
    [Fact]
    public void WallpaperWindow_UsesFillStretchForComputedElementSize()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml.cs"));

        Assert.Contains("CurrentImage.Stretch = Stretch.Fill", source);
        Assert.Contains("NextImage.Stretch = Stretch.Fill", source);
        Assert.Contains("VideoPlayer.Stretch = Stretch.Fill", source);
        Assert.DoesNotContain("Stretch = Stretch.None", source);
    }

    [Fact]
    public void WallpaperWindow_AnchorsWallpaperElementsForCanvasOffsets()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml"));

        AssertWallpaperElementAnchored(source, "CurrentImage");
        AssertWallpaperElementAnchored(source, "NextImage");
        AssertWallpaperElementAnchored(source, "VideoPlayer");
    }

    [Fact]
    public void WallpaperWindow_UsesXamlRootViewportForVideoLayout()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml.cs"));
        string applyVideoLayout = source[
            source.IndexOf("private void ApplyVideoLayout", StringComparison.Ordinal)..
            source.IndexOf("private DoubleAnimation CreateOpacityAnimation", StringComparison.Ordinal)];

        Assert.Contains("GetViewportWidth(Root)", applyVideoLayout);
        Assert.Contains("GetViewportHeight(Root)", applyVideoLayout);
        Assert.DoesNotContain("ActualWidthOrFallback()", applyVideoLayout);
        Assert.DoesNotContain("ActualHeightOrFallback()", applyVideoLayout);
    }

    [Fact]
    public void ShowImageAsync_PreparesNextImageWithoutReapplyingCurrentImageProfile()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml.cs"));
        string showImageAsync = source[
            source.IndexOf("public async Task ShowImageAsync", StringComparison.Ordinal)..
            source.IndexOf("public async Task ShowVideoAsync", StringComparison.Ordinal)];

        Assert.Contains("PrepareNextImageForTransition();", showImageAsync);
        Assert.DoesNotContain("ApplyProfile(_profile);", showImageAsync);
    }

    [Fact]
    public void ShowVideoAsync_GuardsAsyncSourceAssignmentWithRequestVersion()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml.cs"));
        string showVideoAsync = source[
            source.IndexOf("public async Task ShowVideoAsync", StringComparison.Ordinal)..
            source.IndexOf("public void SetVideoPausedByCoverage", StringComparison.Ordinal)];

        Assert.Contains("int requestVersion = BeginMediaRequest();", showVideoAsync);
        Assert.Contains("MediaPlayer player = ReplaceMediaPlayer(loop);", showVideoAsync);
        Assert.Contains("!IsCurrentMediaRequest(requestVersion) || !ReferenceEquals(player, _mediaPlayer)", showVideoAsync);
    }

    [Fact]
    public void ShowVideoAsync_UsesSequentialPlaybackSourceInsteadOfStorageFileSource()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml.cs"));
        string showVideoAsync = source[
            source.IndexOf("public async Task ShowVideoAsync", StringComparison.Ordinal)..
            source.IndexOf("public void SetVideoPausedByCoverage", StringComparison.Ordinal)];

        Assert.Contains("VideoPlaybackSource.Open(path)", showVideoAsync);
        Assert.DoesNotContain("MediaSource.CreateFromStorageFile", showVideoAsync);
        Assert.DoesNotContain("StorageFile.GetFileFromPathAsync", showVideoAsync);
    }

    [Fact]
    public void WallpaperWindow_IgnoresEventsFromReplacedMediaPlayers()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml.cs"));

        Assert.Contains("if (!ReferenceEquals(sender, _mediaPlayer))", source);
        Assert.Contains("if (IsCurrentMediaRequest(requestVersion) && _currentKind == MediaKind.Video && _currentImagePath == path)", source);
    }

    [Fact]
    public void StopVideo_CancelsPendingRequestsAndClearsPlayerSource()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "WallpaperWindow.xaml.cs"));
        string stopVideo = source[
            source.IndexOf("private void StopVideo", StringComparison.Ordinal)..
            source.IndexOf("private void MediaPlayer_MediaEnded", StringComparison.Ordinal)];

        Assert.Contains("CancelMediaRequest();", stopVideo);
        Assert.Contains("ResetMediaPlayerSource(_mediaPlayer);", stopVideo);
        Assert.Contains("VideoPlayer.Visibility = Visibility.Collapsed;", stopVideo);
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

    private static void AssertWallpaperElementAnchored(string source, string name)
    {
        int nameIndex = source.IndexOf($"x:Name=\"{name}\"", StringComparison.Ordinal);
        Assert.True(nameIndex >= 0, $"Could not find {name}.");
        int endIndex = source.IndexOf("/>", nameIndex, StringComparison.Ordinal);
        Assert.True(endIndex > nameIndex, $"Could not find the end of {name}.");
        string element = source[nameIndex..endIndex];

        Assert.Contains("HorizontalAlignment=\"Left\"", element);
        Assert.Contains("VerticalAlignment=\"Top\"", element);
    }
}
