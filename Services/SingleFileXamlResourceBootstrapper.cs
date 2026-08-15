namespace SlideShowWallpaper.Services;

internal static class SingleFileXamlResourceBootstrapper
{
    private const string ResourceIndexFileName = "SlideShowWallpaper.pri";

    public static void EnsureForCurrentProcess()
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return;
        }

        Ensure(AppContext.BaseDirectory, processPath);
    }

    internal static void Ensure(string bundleDirectory, string processPath)
    {
        string processDirectory = Path.GetDirectoryName(processPath) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(processDirectory))
        {
            return;
        }

        string sourcePath = Path.Combine(bundleDirectory, ResourceIndexFileName);
        string targetPath = Path.Combine(processDirectory, ResourceIndexFileName);
        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(sourcePath))
        {
            return;
        }

        string temporaryPath = Path.Combine(
            processDirectory,
            $".{ResourceIndexFileName}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");

        try
        {
            if (File.Exists(targetPath) && File.ReadAllBytes(sourcePath).AsSpan().SequenceEqual(File.ReadAllBytes(targetPath)))
            {
                return;
            }

            File.Copy(sourcePath, temporaryPath, overwrite: true);
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AppLog.Write(exception);
            throw new InvalidOperationException(
                $"Unable to prepare the WinUI resource index beside '{processPath}'.",
                exception);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
