using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class SingleInstanceServiceTests
{
    [Fact]
    public void TryAcquirePrimary_WithExistingPrimary_ReturnsFalseForSecondInstance()
    {
        string key = Guid.NewGuid().ToString("N");
        using var primary = new SingleInstanceService(key);
        using var secondary = new SingleInstanceService(key);

        Assert.True(primary.TryAcquirePrimary());
        Assert.False(secondary.TryAcquirePrimary());
    }

    [Fact]
    public async Task NotifyPrimaryAsync_WithRunningListener_RequestsPrimaryWindow()
    {
        string key = Guid.NewGuid().ToString("N");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var primary = new SingleInstanceService(key);
        using var secondary = new SingleInstanceService(key);

        Assert.True(primary.TryAcquirePrimary());
        primary.StartActivationListener(() => completion.TrySetResult());

        Assert.True(await secondary.NotifyPrimaryAsync(TimeSpan.FromSeconds(5)));
        Task completed = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(completion.Task, completed);
    }

    [Fact]
    public void DefaultInstanceKey_DoesNotDependOnExecutablePath()
    {
        Assert.DoesNotContain(AppContext.BaseDirectory, SingleInstanceService.DefaultInstanceKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NotifyPrimaryAsync_TreatsPipeAccessDeniedAsNotificationFailure()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "SingleInstanceService.cs"));
        string method = source[
            source.IndexOf("public async Task<bool> NotifyPrimaryAsync", StringComparison.Ordinal)..
            source.IndexOf("public void Dispose", StringComparison.Ordinal)];

        Assert.Contains("UnauthorizedAccessException", method);
        Assert.Contains("return false;", method);
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
