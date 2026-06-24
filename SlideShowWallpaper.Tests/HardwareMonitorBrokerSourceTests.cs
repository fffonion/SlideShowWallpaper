namespace SlideShowWallpaper.Tests;

public sealed class HardwareMonitorBrokerSourceTests
{
    [Fact]
    public void BrokerClient_StartsNamedBrokerExecutable()
    {
        string root = FindProjectRoot();
        string clientSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerClient.cs"));
        string executableSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerExecutable.cs"));

        Assert.Contains("SlideShowWallpaper.HardwareBroker.exe", executableSource);
        Assert.Contains("HardwareMonitorBrokerExecutable.GetBrokerExecutablePath(processPath)", clientSource);
        Assert.Contains("FileName = brokerPath", clientSource);
        Assert.Contains("WorkingDirectory = Path.GetDirectoryName(brokerPath)", clientSource);
    }

    [Fact]
    public void BrokerHost_ExitsWhenNoRequestsArriveBeforeIdleTimeout()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerHost.cs"));
        string method = source[
            source.IndexOf("private static void RunServer", StringComparison.Ordinal)..
            source.IndexOf("private static bool HandleRequest", StringComparison.Ordinal)];

        Assert.Contains("IdleExitTimeout", source);
        Assert.Contains("CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)", method);
        Assert.Contains("idleCancellation.CancelAfter(IdleExitTimeout);", method);
        Assert.Contains("server.WaitForConnectionAsync(idleCancellation.Token)", method);
        Assert.Contains("when (!cancellationToken.IsCancellationRequested)", method);
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
