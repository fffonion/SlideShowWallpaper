using System.Net;
using System.Text;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class AppUpdateInstallerServiceTests
{
    [Fact]
    public async Task PrepareUpdateAsync_WithHttpsExe_DownloadsUpdateAndCreatesReplaceScript()
    {
        string tempRoot = CreateTempDirectory();
        string appPath = Path.Combine(tempRoot, "app", "SlideShowWallpaper.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(appPath)!);
        await File.WriteAllTextAsync(appPath, "old");
        using var handler = new StubHttpMessageHandler("new exe");
        using var httpClient = new HttpClient(handler);
        var service = new AppUpdateInstallerService(httpClient, appPath, tempRoot, currentProcessId: 1234);

        AppUpdateInstallPlan plan = await service.PrepareUpdateAsync(new Uri("https://example.com/SlideShowWallpaper.exe"), CancellationToken.None);

        Assert.True(File.Exists(plan.DownloadPath));
        Assert.Equal("new exe", await File.ReadAllTextAsync(plan.DownloadPath));
        string script = await File.ReadAllTextAsync(plan.ScriptPath);
        Assert.Contains("Wait-Process -Id 1234", script);
        Assert.Contains("Copy-Item -LiteralPath", script);
        Assert.Contains(EscapePowerShellSingleQuotedString(appPath), script);
        Assert.Contains("Remove-Item -LiteralPath $source", script);
        Assert.Contains("Start-Process -FilePath", script);
        Assert.Contains("Remove-Item -LiteralPath $PSCommandPath", script);
    }

    [Fact]
    public async Task PrepareUpdateAsync_WithNonHttpsUrl_RejectsDownload()
    {
        string tempRoot = CreateTempDirectory();
        string appPath = Path.Combine(tempRoot, "SlideShowWallpaper.exe");
        using var httpClient = new HttpClient(new StubHttpMessageHandler("new exe"));
        var service = new AppUpdateInstallerService(httpClient, appPath, tempRoot, currentProcessId: 1234);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareUpdateAsync(new Uri("http://example.com/SlideShowWallpaper.exe"), CancellationToken.None));
    }

    [Fact]
    public async Task PrepareUpdateAsync_WithNonExeAsset_RejectsDownload()
    {
        string tempRoot = CreateTempDirectory();
        string appPath = Path.Combine(tempRoot, "SlideShowWallpaper.exe");
        using var httpClient = new HttpClient(new StubHttpMessageHandler("new exe"));
        var service = new AppUpdateInstallerService(httpClient, appPath, tempRoot, currentProcessId: 1234);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PrepareUpdateAsync(new Uri("https://example.com/SlideShowWallpaper.zip"), CancellationToken.None));
    }

    [Fact]
    public void StartUpdater_UsesHiddenPowerShellProcess()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "AppUpdateInstallerService.cs"));

        Assert.Contains("FileName = \"powershell.exe\"", source);
        Assert.Contains("UseShellExecute = false", source);
        Assert.Contains("CreateNoWindow = true", source);
        Assert.Contains("WindowStyle = ProcessWindowStyle.Hidden", source);
        Assert.Contains("startInfo.ArgumentList.Add(\"-WindowStyle\")", source);
        Assert.Contains("startInfo.ArgumentList.Add(\"Hidden\")", source);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "SlideShowWallpaperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
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

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _content;

        public StubHttpMessageHandler(string content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/octet-stream"),
            });
        }
    }
}
