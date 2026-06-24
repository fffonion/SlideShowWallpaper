using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlideShowWallpaper.Services;

public enum UpdateCheckStatus
{
    UpdateAvailable,
    UpToDate,
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    Version CurrentVersion,
    Version? LatestVersion,
    string LatestTag,
    string ReleaseUrl,
    string DownloadUrl,
    string ErrorMessage)
{
    public static UpdateCheckResult Failed(Version currentVersion, string errorMessage)
    {
        return new UpdateCheckResult(
            UpdateCheckStatus.Failed,
            currentVersion,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            errorMessage);
    }
}

public sealed class GitHubReleaseUpdateService : IDisposable
{
    private static readonly Uri DefaultLatestReleaseUri = new("https://api.github.com/repos/fffonion/SlideShowWallpaper/releases/latest");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly Uri _latestReleaseUri;
    private readonly bool _disposeHttpClient;

    public GitHubReleaseUpdateService()
        : this(new HttpClient(), DefaultLatestReleaseUri, disposeHttpClient: true)
    {
    }

    public GitHubReleaseUpdateService(HttpClient httpClient, Uri latestReleaseUri)
        : this(httpClient, latestReleaseUri, disposeHttpClient: false)
    {
    }

    private GitHubReleaseUpdateService(HttpClient httpClient, Uri latestReleaseUri, bool disposeHttpClient)
    {
        _httpClient = httpClient;
        _latestReleaseUri = latestReleaseUri;
        _disposeHttpClient = disposeHttpClient;
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(Version currentVersion, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _latestReleaseUri);
            request.Headers.UserAgent.Add(ProductInfoHeaderValue.Parse("SlideShowWallpaper/UpdateChecker"));
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/vnd.github+json"));
            request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Failed(currentVersion, response.ReasonPhrase ?? response.StatusCode.ToString());
            }

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            GitHubReleaseDto? release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(stream, JsonOptions, cancellationToken);
            if (release is null || !TryParseReleaseVersion(release.TagName, out Version? latestVersion))
            {
                return UpdateCheckResult.Failed(currentVersion, "Unable to parse the latest release.");
            }

            string downloadUrl = release.Assets
                .FirstOrDefault(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl ?? string.Empty;
            UpdateCheckStatus status = latestVersion > currentVersion ? UpdateCheckStatus.UpdateAvailable : UpdateCheckStatus.UpToDate;
            return new UpdateCheckResult(
                status,
                currentVersion,
                latestVersion,
                release.TagName,
                release.HtmlUrl,
                downloadUrl,
                string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return UpdateCheckResult.Failed(currentVersion, exception.Message);
        }
    }

    public static bool TryParseReleaseVersion(string value, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        int start = 0;
        while (start < span.Length && !char.IsDigit(span[start]))
        {
            start++;
        }

        if (start >= span.Length)
        {
            return false;
        }

        int length = 0;
        while (start + length < span.Length)
        {
            char current = span[start + length];
            if (!char.IsDigit(current) && current != '.')
            {
                break;
            }

            length++;
        }

        return Version.TryParse(span.Slice(start, length).ToString(), out version);
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAssetDto> Assets { get; set; } = [];
    }

    private sealed class GitHubReleaseAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}

public static class AppVersionService
{
    public static Version GetCurrentVersion()
    {
        return GetCurrentVersion(typeof(AppVersionService).Assembly);
    }

    public static Version GetCurrentVersion(Assembly assembly)
    {
        AssemblyInformationalVersionAttribute? attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute is not null
            && GitHubReleaseUpdateService.TryParseReleaseVersion(attribute.InformationalVersion, out Version? version)
            && version is not null)
        {
            return version;
        }

        return assembly.GetName().Version ?? new Version(1, 0, 0);
    }
}
