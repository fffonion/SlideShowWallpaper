namespace SlideShowWallpaper.Services;

public static class HardwareBrokerExecutableResolver
{
    public const string BrokerExecutableFileName = "SlideShowWallpaper.HardwareBroker.exe";
    private const string BrokerResourceName = BrokerExecutableFileName;

    public static string GetBrokerExecutablePath(string processPath)
    {
        _ = processPath;
        string brokerPath = Path.Combine(AppTempPaths.Broker, BrokerExecutableFileName);
        return TryExtractBrokerExecutable(brokerPath, allowProcessFallbackPath: true);
    }

    private static string TryExtractBrokerExecutable(string brokerPath, bool allowProcessFallbackPath)
    {
        try
        {
            using Stream? resource = typeof(HardwareBrokerExecutableResolver).Assembly.GetManifestResourceStream(BrokerResourceName);
            if (resource is null)
            {
                return string.Empty;
            }

            Directory.CreateDirectory(AppTempPaths.Broker);
            using var memory = new MemoryStream();
            resource.CopyTo(memory);
            byte[] brokerBytes = memory.ToArray();
            if (TryWriteBrokerExecutable(brokerPath, brokerBytes))
            {
                return brokerPath;
            }

            if (!allowProcessFallbackPath)
            {
                return string.Empty;
            }

            string fallbackBrokerPath = Path.Combine(
                AppTempPaths.Broker,
                Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                BrokerExecutableFileName);
            return TryWriteBrokerExecutable(fallbackBrokerPath, brokerBytes) ? fallbackBrokerPath : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static bool TryWriteBrokerExecutable(string brokerPath, byte[] brokerBytes)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(brokerPath)!);
            string temporaryPath = brokerPath + ".tmp";
            File.WriteAllBytes(temporaryPath, brokerBytes);
            File.Move(temporaryPath, brokerPath, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
