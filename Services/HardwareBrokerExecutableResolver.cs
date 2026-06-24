namespace SlideShowWallpaper.Services;

public static class HardwareBrokerExecutableResolver
{
    public const string BrokerExecutableFileName = "SlideShowWallpaper.HardwareBroker.exe";
    private const string BrokerResourceName = BrokerExecutableFileName;

    public static string GetBrokerExecutablePath(string processPath)
    {
        string brokerPath = Path.Combine(AppTempPaths.Broker, BrokerExecutableFileName);
        return TryExtractBrokerExecutable(brokerPath) ? brokerPath : processPath;
    }

    private static bool TryExtractBrokerExecutable(string brokerPath)
    {
        try
        {
            using Stream? resource = typeof(HardwareBrokerExecutableResolver).Assembly.GetManifestResourceStream(BrokerResourceName);
            if (resource is null)
            {
                return File.Exists(brokerPath);
            }

            Directory.CreateDirectory(AppTempPaths.Broker);
            using var memory = new MemoryStream();
            resource.CopyTo(memory);
            byte[] brokerBytes = memory.ToArray();
            string temporaryPath = brokerPath + ".tmp";
            File.WriteAllBytes(temporaryPath, brokerBytes);
            File.Move(temporaryPath, brokerPath, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return File.Exists(brokerPath);
        }
        catch (UnauthorizedAccessException)
        {
            return File.Exists(brokerPath);
        }
    }
}
