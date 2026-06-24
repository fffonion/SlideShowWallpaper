namespace SlideShowWallpaper.Tests;

public sealed class HardwareMonitorBrokerSourceTests
{
    [Fact]
    public void BrokerClient_StartsCurrentExecutableDirectly()
    {
        string root = FindProjectRoot();
        string clientSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerClient.cs"));

        Assert.False(File.Exists(Path.Combine(root, "Services", "HardwareMonitorBrokerExecutable.cs")));
        Assert.DoesNotContain("HardwareMonitorBrokerExecutable", clientSource);
        Assert.Contains("FileName = processPath", clientSource);
        Assert.Contains("WorkingDirectory = Path.GetDirectoryName(processPath)", clientSource);
    }

    [Fact]
    public void BrokerHost_SetsBrokerProcessTitleBeforeServingRequests()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerHost.cs"));
        string method = source[
            source.IndexOf("public static int Run", StringComparison.Ordinal)..
            source.IndexOf("private static void RunServer", StringComparison.Ordinal)];

        Assert.Contains("SlideShowWallpaper Hardware Broker", source);
        Assert.Contains("SetBrokerProcessTitle();", method);
        Assert.Contains("Console.Title = BrokerProcessTitle;", source);
    }

    [Fact]
    public void BrokerHost_WaitsForRequestsUntilParentExitsOrShutdown()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerHost.cs"));
        string method = source[
            source.IndexOf("private static void RunServer", StringComparison.Ordinal)..
            source.IndexOf("private static bool HandleRequest", StringComparison.Ordinal)];

        Assert.DoesNotContain("IdleExitTimeout", source);
        Assert.DoesNotContain("CancelAfter", method);
        Assert.DoesNotContain("when (!cancellationToken.IsCancellationRequested)", method);
        Assert.Contains("server.WaitForConnectionAsync(cancellationToken)", method);
        Assert.Contains("HandleRequest(server, reader, cancellationToken)", method);
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
