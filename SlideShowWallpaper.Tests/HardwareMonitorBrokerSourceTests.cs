namespace SlideShowWallpaper.Tests;

public sealed class HardwareMonitorBrokerSourceTests
{
    [Fact]
    public void MainAssembly_EmbedsHardwareBrokerExecutableResource()
    {
        using Stream? resource = typeof(Services.HardwareBrokerExecutableResolver)
            .Assembly
            .GetManifestResourceStream(Services.HardwareBrokerExecutableResolver.BrokerExecutableFileName);
        Assert.NotNull(resource);

        Span<byte> header = stackalloc byte[2];
        Assert.Equal(2, resource.Read(header));
        Assert.Equal((byte)'M', header[0]);
        Assert.Equal((byte)'Z', header[1]);
    }

    [Fact]
    public void BrokerExecutableResolver_ExtractsBrokerWithDistinctDescription()
    {
        string processPath = Environment.ProcessPath ?? "testhost.exe";
        string brokerPath = Services.HardwareBrokerExecutableResolver.GetBrokerExecutablePath(processPath);

        Assert.EndsWith(Services.HardwareBrokerExecutableResolver.BrokerExecutableFileName, brokerPath);
        Assert.True(File.Exists(brokerPath));
        var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(brokerPath);
        Assert.Equal("Codex 壁纸大师 Broker", versionInfo.FileDescription);
    }

    [Fact]
    public void BrokerClient_StartsEmbeddedBrokerExecutable()
    {
        string root = FindProjectRoot();
        string clientSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerClient.cs"));

        Assert.Contains("HardwareBrokerExecutableResolver.GetBrokerExecutablePath(processPath)", clientSource);
        Assert.Contains("FileName = brokerPath", clientSource);
        Assert.Contains("WorkingDirectory = Path.GetDirectoryName(brokerPath)", clientSource);
    }

    [Fact]
    public void BrokerExecutableResolver_ExtractsEmbeddedBrokerWithoutCopying()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareBrokerExecutableResolver.cs"));

        Assert.Contains("SlideShowWallpaper.HardwareBroker.exe", source);
        Assert.Contains("GetManifestResourceStream(BrokerResourceName)", source);
        Assert.Contains("AppTempPaths.Broker", source);
        Assert.Contains("File.WriteAllBytes(temporaryPath, brokerBytes)", source);
        Assert.DoesNotContain("File.Copy", source);
        Assert.DoesNotContain("CreateHardLink", source);
        Assert.DoesNotContain("FileInfo", source);
        Assert.DoesNotContain("SHA", source);
    }

    [Fact]
    public void BrokerHost_DoesNotUseConsoleTitleForTaskManagerName()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerHost.cs"));

        Assert.DoesNotContain("Console.Title", source);
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

    [Fact]
    public void HardwareBrokerProject_HasDistinctDescriptionAndNoWinUi()
    {
        string root = FindProjectRoot();
        string projectPath = Path.Combine(root, "HardwareBroker", "SlideShowWallpaper.HardwareBroker.csproj");
        string source = File.ReadAllText(projectPath);

        Assert.Contains("<AssemblyName>SlideShowWallpaper.HardwareBroker</AssemblyName>", source);
        Assert.Contains("<FileDescription>Codex 壁纸大师 Broker</FileDescription>", source);
        Assert.Contains("LibreHardwareMonitorLib", source);
        Assert.DoesNotContain("UseWinUI>true", source);
        Assert.DoesNotContain("Microsoft.WindowsAppSDK", source);
    }

    [Fact]
    public void MainProject_BuildsAndPublishesHardwareBroker()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "SlideShowWallpaper.csproj"));

        Assert.Contains("HardwareBrokerProject", source);
        Assert.Contains("BuildHardwareBroker", source);
        Assert.Contains("EmbeddedResource", source);
        Assert.Contains("SlideShowWallpaper.HardwareBroker.exe", source);
        Assert.DoesNotContain("PublishHardwareBrokerSingleFile", source);
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
