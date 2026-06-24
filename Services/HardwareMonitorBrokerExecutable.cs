namespace SlideShowWallpaper.Services;

public static class HardwareMonitorBrokerExecutable
{
    public const string BrokerExecutableFileName = "SlideShowWallpaper.HardwareBroker.exe";

    public static string GetBrokerExecutablePath(string processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return processPath;
        }

        string sourceDirectory = Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
        string targetDirectory = IsLooseBuild(processPath)
            ? sourceDirectory
            : AppTempPaths.Broker;
        string brokerPath = Path.Combine(targetDirectory, BrokerExecutableFileName);

        return TryPrepareBrokerExecutable(processPath, brokerPath) ? brokerPath : processPath;
    }

    private static bool TryPrepareBrokerExecutable(string sourcePath, string brokerPath)
    {
        try
        {
            if (Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(brokerPath), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var source = new FileInfo(sourcePath);
            var broker = new FileInfo(brokerPath);
            if (broker.Exists
                && broker.Length == source.Length
                && broker.LastWriteTimeUtc == source.LastWriteTimeUtc)
            {
                return true;
            }

            Directory.CreateDirectory(broker.DirectoryName ?? AppTempPaths.Broker);
            if (broker.Exists)
            {
                broker.Delete();
            }

            File.Copy(source.FullName, broker.FullName);
            File.SetLastWriteTimeUtc(broker.FullName, source.LastWriteTimeUtc);
            return true;
        }
        catch (IOException exception)
        {
            AppLog.Write(exception);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            AppLog.Write(exception);
            return false;
        }
    }

    private static bool IsLooseBuild(string processPath)
    {
        string directory = Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
        string assemblyPath = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(processPath)}.dll");
        return File.Exists(assemblyPath);
    }
}
