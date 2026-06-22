using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class FolderChangeWatcherServiceTests
{
    [Fact]
    public async Task Watch_WithNewSupportedImage_RaisesChangeOnceAfterDebounce()
    {
        string folder = CreateTempFolder();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FolderChangeWatcherService(TimeSpan.FromMilliseconds(50));

        watcher.Watch("display1", folder, () => completion.TrySetResult());
        await File.WriteAllTextAsync(Path.Combine(folder, "new.png"), string.Empty);

        Task completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(completion.Task, completed);
    }

    [Fact]
    public async Task Watch_WithUnsupportedFile_DoesNotRaiseChange()
    {
        string folder = CreateTempFolder();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new FolderChangeWatcherService(TimeSpan.FromMilliseconds(50));

        watcher.Watch("display1", folder, () => completion.TrySetResult());
        await File.WriteAllTextAsync(Path.Combine(folder, "note.txt"), string.Empty);

        Task completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromMilliseconds(500)));
        Assert.NotSame(completion.Task, completed);
    }

    private static string CreateTempFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
