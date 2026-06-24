using System.Net;
using System.Reflection;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class GitHubReleaseUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_WithNewerRelease_ReturnsReleaseAndAssetLink()
    {
        using var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "tag_name": "v1.6.0",
              "html_url": "https://github.com/fffonion/SlideShowWallpaper/releases/tag/v1.6.0",
              "assets": [
                {
                  "name": "SlideShowWallpaper-v1.6.0-win-x64.exe",
                  "browser_download_url": "https://github.com/fffonion/SlideShowWallpaper/releases/download/v1.6.0/SlideShowWallpaper-v1.6.0-win-x64.exe"
                }
              ]
            }
            """);
        using var httpClient = new HttpClient(handler);
        var service = new GitHubReleaseUpdateService(
            httpClient,
            new Uri("https://api.github.com/repos/fffonion/SlideShowWallpaper/releases/latest"));

        UpdateCheckResult result = await service.CheckForUpdateAsync(new Version(1, 5, 0), CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new Version(1, 6, 0), result.LatestVersion);
        Assert.Equal("v1.6.0", result.LatestTag);
        Assert.Equal("https://github.com/fffonion/SlideShowWallpaper/releases/tag/v1.6.0", result.ReleaseUrl);
        Assert.Equal("https://github.com/fffonion/SlideShowWallpaper/releases/download/v1.6.0/SlideShowWallpaper-v1.6.0-win-x64.exe", result.DownloadUrl);
        Assert.Equal("SlideShowWallpaper/UpdateChecker", handler.UserAgent);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WithSameRelease_ReturnsUpToDate()
    {
        using var handler = new StubHttpMessageHandler(
            HttpStatusCode.OK,
            """
            {
              "tag_name": "v1.5.0",
              "html_url": "https://github.com/fffonion/SlideShowWallpaper/releases/tag/v1.5.0",
              "assets": []
            }
            """);
        using var httpClient = new HttpClient(handler);
        var service = new GitHubReleaseUpdateService(
            httpClient,
            new Uri("https://api.github.com/repos/fffonion/SlideShowWallpaper/releases/latest"));

        UpdateCheckResult result = await service.CheckForUpdateAsync(new Version(1, 5, 0), CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Equal(new Version(1, 5, 0), result.LatestVersion);
        Assert.Equal(string.Empty, result.DownloadUrl);
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("release-1.2.3", "1.2.3")]
    [InlineData("v1.2.3-beta.1", "1.2.3")]
    public void TryParseReleaseVersion_WithCommonTagFormats_ParsesVersion(string tag, string expected)
    {
        Assert.True(GitHubReleaseUpdateService.TryParseReleaseVersion(tag, out Version? version));
        Assert.Equal(Version.Parse(expected), version);
    }

    [Fact]
    public void GetCurrentVersion_UsesAssemblyInformationalVersion()
    {
        Version version = AppVersionService.GetCurrentVersion(typeof(GitHubReleaseUpdateServiceTests).Assembly);
        Version expected = typeof(GitHubReleaseUpdateServiceTests).Assembly.GetName().Version ?? new Version(1, 0, 0);
        AssemblyInformationalVersionAttribute? attribute = typeof(GitHubReleaseUpdateServiceTests).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute is not null
            && GitHubReleaseUpdateService.TryParseReleaseVersion(attribute.InformationalVersion, out Version? parsedVersion)
            && parsedVersion is not null)
        {
            expected = parsedVersion;
        }

        Assert.Equal(expected, version);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler, IDisposable
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        public string UserAgent { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UserAgent = request.Headers.UserAgent.ToString();
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content),
            });
        }
    }
}
