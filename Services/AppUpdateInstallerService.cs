using System.Diagnostics;
using System.Text;

namespace SlideShowWallpaper.Services;

public sealed record AppUpdateInstallPlan(string DownloadPath, string ScriptPath);

public sealed class AppUpdateInstallerService : IDisposable
{
    private const string UpdateDirectoryName = "SlideShowWallpaper\\Updates";
    private readonly HttpClient _httpClient;
    private readonly string _appExecutablePath;
    private readonly string _updateRootDirectory;
    private readonly int _currentProcessId;
    private readonly bool _disposeHttpClient;

    public AppUpdateInstallerService()
        : this(
            new HttpClient(),
            Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty,
            Path.Combine(Path.GetTempPath(), UpdateDirectoryName),
            Environment.ProcessId,
            disposeHttpClient: true)
    {
    }

    public AppUpdateInstallerService(HttpClient httpClient, string appExecutablePath, string updateRootDirectory, int currentProcessId)
        : this(httpClient, appExecutablePath, updateRootDirectory, currentProcessId, disposeHttpClient: false)
    {
    }

    private AppUpdateInstallerService(HttpClient httpClient, string appExecutablePath, string updateRootDirectory, int currentProcessId, bool disposeHttpClient)
    {
        _httpClient = httpClient;
        _appExecutablePath = appExecutablePath;
        _updateRootDirectory = updateRootDirectory;
        _currentProcessId = currentProcessId;
        _disposeHttpClient = disposeHttpClient;
    }

    public async Task<AppUpdateInstallPlan> PrepareUpdateAsync(Uri downloadUri, CancellationToken cancellationToken)
    {
        ValidateDownloadUri(downloadUri);
        if (string.IsNullOrWhiteSpace(_appExecutablePath))
        {
            throw new InvalidOperationException("Current executable path is unavailable.");
        }

        Directory.CreateDirectory(_updateRootDirectory);
        string updateId = Guid.NewGuid().ToString("N");
        string downloadPath = Path.Combine(_updateRootDirectory, $"SlideShowWallpaper-{updateId}.exe");
        string scriptPath = Path.Combine(_updateRootDirectory, $"ApplyUpdate-{updateId}.ps1");

        using HttpResponseMessage response = await _httpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using (Stream source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1024 * 128, useAsync: true))
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        if (new FileInfo(downloadPath).Length == 0)
        {
            File.Delete(downloadPath);
            throw new InvalidOperationException("Downloaded update is empty.");
        }

        await File.WriteAllTextAsync(scriptPath, CreateUpdaterScript(downloadPath, _appExecutablePath, _currentProcessId), Encoding.UTF8, cancellationToken);
        return new AppUpdateInstallPlan(downloadPath, scriptPath);
    }

    public void StartUpdater(AppUpdateInstallPlan plan)
    {
        if (!File.Exists(plan.DownloadPath) || !File.Exists(plan.ScriptPath))
        {
            throw new FileNotFoundException("Prepared update files are missing.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-WindowStyle");
        startInfo.ArgumentList.Add("Hidden");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(plan.ScriptPath);
        Process.Start(startInfo)?.Dispose();
    }

    public void Dispose()
    {
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static void ValidateDownloadUri(Uri downloadUri)
    {
        if (!downloadUri.IsAbsoluteUri || downloadUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Update download URL must use HTTPS.");
        }

        if (!downloadUri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Update download URL must point to an executable.");
        }
    }

    private static string CreateUpdaterScript(string downloadPath, string appExecutablePath, int currentProcessId)
    {
        string source = EscapePowerShellSingleQuotedString(downloadPath);
        string target = EscapePowerShellSingleQuotedString(appExecutablePath);
        return $$"""
            $ErrorActionPreference = 'Stop'
            $source = '{{source}}'
            $target = '{{target}}'
            Wait-Process -Id {{currentProcessId}} -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 500
            Copy-Item -LiteralPath $source -Destination $target -Force
            Remove-Item -LiteralPath $source -Force -ErrorAction SilentlyContinue
            Start-Process -FilePath $target
            Remove-Item -LiteralPath $PSCommandPath -Force -ErrorAction SilentlyContinue
            """;
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
