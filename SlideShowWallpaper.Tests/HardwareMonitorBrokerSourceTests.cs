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
        using Stream resource = typeof(Services.HardwareBrokerExecutableResolver)
            .Assembly
            .GetManifestResourceStream(Services.HardwareBrokerExecutableResolver.BrokerExecutableFileName)
            ?? throw new InvalidOperationException("Broker resource is missing.");
        string brokerPath = Path.Combine(
            Path.GetTempPath(),
            $"SlideShowWallpaper.HardwareBroker.{Guid.NewGuid():N}.exe");
        using (var output = File.Create(brokerPath))
        {
            resource.CopyTo(output);
        }

        Assert.True(File.Exists(brokerPath));
        var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(brokerPath);
        Assert.Equal("SlideShowWallpaper Broker", versionInfo.FileDescription);
        File.Delete(brokerPath);
    }

    [Fact]
    public void BrokerClient_StartsEmbeddedBrokerExecutable()
    {
        string root = FindProjectRoot();
        string clientSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerClient.cs"));

        Assert.Contains("HardwareBrokerExecutableResolver.GetBrokerExecutablePath(processPath)", clientSource);
        Assert.Contains("string.IsNullOrWhiteSpace(brokerPath)", clientSource);
        Assert.Contains("FileName = brokerPath", clientSource);
        Assert.Contains("WorkingDirectory = Path.GetDirectoryName(brokerPath)", clientSource);
        Assert.Contains("if (requestElevation && !CurrentProcessPrivilege.IsElevated())", clientSource);
        Assert.Contains("startInfo.Verb = \"runas\";", clientSource);
    }

    [Fact]
    public void BrokerClient_RaisesBrokerProcessStartedAfterProcessStart()
    {
        string root = FindProjectRoot();
        string clientSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerClient.cs"));

        int processStartIndex = clientSource.IndexOf("_brokerProcess = StartBrokerProcess(", StringComparison.Ordinal);
        int eventIndex = clientSource.IndexOf("BrokerProcessStarted?.Invoke", processStartIndex, StringComparison.Ordinal);

        Assert.Contains("public event EventHandler? BrokerProcessStarted;", clientSource);
        Assert.True(processStartIndex >= 0);
        Assert.True(eventIndex > processStartIndex);
    }

    [Fact]
    public void BrokerClient_EmptySnapshotDoesNotReportMainProcessElevation()
    {
        string root = FindProjectRoot();
        string clientSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerClient.cs"));
        string method = clientSource[
            clientSource.IndexOf("private static HardwareMonitorSnapshot EmptySnapshot", StringComparison.Ordinal)..
            clientSource.IndexOf("private static IReadOnlyList<string> CreateRuntimeSensorIds", StringComparison.Ordinal)];

        Assert.Contains("new HardwareMonitorSnapshot([], DateTimeOffset.Now, IsElevated: false)", method);
        Assert.DoesNotContain("CurrentProcessPrivilege.IsAdministrator", method);
    }

    [Fact]
    public void HardwareMonitorService_ForwardsBrokerProcessStarted()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorService.cs"));

        Assert.Contains("public event EventHandler? BrokerProcessStarted;", source);
        Assert.Contains("_brokerClient.BrokerProcessStarted += BrokerClient_BrokerProcessStarted;", source);
        Assert.Contains("BrokerProcessStarted?.Invoke(this, args);", source);
        Assert.Contains("_brokerClient.BrokerProcessStarted -= BrokerClient_BrokerProcessStarted;", source);
    }

    [Fact]
    public void BrokerExecutableResolver_ExtractsEmbeddedBrokerWithoutCopyingOrFallingBackToMainExe()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareBrokerExecutableResolver.cs"));

        Assert.Contains("SlideShowWallpaper.HardwareBroker.exe", source);
        Assert.Contains("GetManifestResourceStream(BrokerResourceName)", source);
        Assert.Contains("AppTempPaths.Broker", source);
        Assert.Contains("File.WriteAllBytes(temporaryPath, brokerBytes)", source);
        Assert.Contains("TryExtractBrokerExecutable(brokerPath, allowProcessFallbackPath: true)", source);
        Assert.Contains("Environment.ProcessId.ToString", source);
        Assert.Contains("return string.Empty;", source);
        Assert.DoesNotContain("? brokerPath : processPath", source);
        Assert.DoesNotContain("File.Copy", source);
        Assert.DoesNotContain("CreateHardLink", source);
        Assert.DoesNotContain("FileInfo", source);
        Assert.DoesNotContain("SHA", source);
    }

    [Fact]
    public void AppTempPaths_CleansOldBrokerFiles()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "AppTempPaths.cs"));

        Assert.Contains("DeleteOldFiles(Broker, cutoffUtc);", source);
    }

    [Fact]
    public void BrokerHost_DoesNotTrimWorkingSetAfterEachRequest()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerHost.cs"));

        Assert.DoesNotContain("TrimBrokerWorkingSet", source);
        Assert.DoesNotContain("WorkingSetTrimmer", source);
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
        Assert.DoesNotContain("using var server = new NamedPipeServerStream", method);
        Assert.Contains("using NamedPipeServerStream server = CreateServer(pipeName);", method);
        Assert.Contains("server.WaitForConnectionAsync(cancellationToken)", method);
        Assert.Contains("HandleRequest(server, reader, cancellationToken)", method);
        Assert.Contains("DisconnectServer(server);", method);
    }

    [Fact]
    public void HardwareBrokerProject_IsConsoleBrokerProjectWithDistinctDescriptionAndNoWinUi()
    {
        string root = FindProjectRoot();
        string projectPath = Path.Combine(root, "HardwareBroker", "SlideShowWallpaper.HardwareBroker.csproj");
        string source = File.ReadAllText(projectPath);

        Assert.Contains("<OutputType>Exe</OutputType>", source);
        Assert.Contains("<TargetFramework>net10.0-windows</TargetFramework>", source);
        Assert.Contains("<RootNamespace>SlideShowWallpaper.HardwareBroker</RootNamespace>", source);
        Assert.Contains("<AssemblyName>SlideShowWallpaper.HardwareBroker</AssemblyName>", source);
        Assert.Contains("<AssemblyTitle>SlideShowWallpaper Broker</AssemblyTitle>", source);
        Assert.Contains("<FileDescription>SlideShowWallpaper Broker</FileDescription>", source);
        Assert.Contains("<Product>SlideShowWallpaper Broker</Product>", source);
        Assert.Contains("<ServerGarbageCollection>false</ServerGarbageCollection>", source);
        Assert.Contains("<InvariantGlobalization>true</InvariantGlobalization>", source);
        Assert.Contains("LibreHardwareMonitorLib", source);
        Assert.DoesNotContain("WorkingSetTrimmer.cs", source);
        Assert.DoesNotContain("<OutputType>WinExe</OutputType>", source);
        Assert.DoesNotContain("windows10.0", source);
        Assert.DoesNotContain("Microsoft.Windows.SDK.NET", source);
        Assert.DoesNotContain("UseWinUI>true", source);
        Assert.DoesNotContain("Microsoft.WindowsAppSDK", source);
    }

    [Fact]
    public void MainProject_BuildsAndPublishesHardwareBroker()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "SlideShowWallpaper.csproj"));

        Assert.Contains("HardwareBrokerProject", source);
        Assert.Contains("HardwareBrokerTargetFramework", source);
        Assert.Contains("TargetFramework=$(HardwareBrokerTargetFramework)", source);
        Assert.Contains("IncludeNativeLibrariesForSelfExtract=true", source);
        Assert.Contains("InvariantGlobalization=true", source);
        Assert.Contains("BuildHardwareBroker", source);
        Assert.Contains("EmbeddedResource", source);
        Assert.Contains("SlideShowWallpaper.HardwareBroker.exe", source);
        Assert.DoesNotContain("PublishHardwareBrokerSingleFile", source);
    }

    [Fact]
    public void BrokerProtocol_AllowsRuntimeSensorFiltering()
    {
        string root = FindProjectRoot();
        string protocolSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerProtocol.cs"));
        string hostSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerHost.cs"));
        string clientSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerClient.cs"));
        string coordinatorSource = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));

        Assert.Contains("public List<string> SensorIds { get; set; } = [];", protocolSource);
        Assert.Contains("reader.GetSnapshot(request?.SensorIds)", hostSource);
        Assert.Contains("CreateRuntimeSensorIds(config)", clientSource);
        Assert.Contains("RestartBrokerWhenSensorFilterChanged(sensorIds)", clientSource);
        Assert.Contains("SensorIds = sensorIds?.ToList() ?? []", clientSource);
        Assert.Contains("OrderBy(sensorId => sensorId, StringComparer.OrdinalIgnoreCase)", clientSource);
        Assert.Contains("_hardwareMonitorService.GetSnapshot(_hardwareMonitorConfig)", coordinatorSource);
    }

    [Fact]
    public void BrokerClient_RestartsBrokerWhenRuntimeSensorFilterChanges()
    {
        string root = FindProjectRoot();
        string clientSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerClient.cs"));

        Assert.Contains("private string[] _lastSnapshotSensorIds = [];", clientSource);
        Assert.Contains("private bool _hasSnapshotSensorIds;", clientSource);
        Assert.Contains("!_usesExternalBroker", clientSource);
        Assert.Contains("!normalizedSensorIds.SequenceEqual(_lastSnapshotSensorIds, StringComparer.OrdinalIgnoreCase)", clientSource);
        Assert.Contains("TrySendShutdown();", clientSource);
        Assert.Contains("DisposeBrokerProcess();", clientSource);
        Assert.Contains("_lastSnapshotSensorIds = normalizedSensorIds;", clientSource);
    }

    [Fact]
    public void BrokerClient_CanAdoptPrestartedBrokerPipe()
    {
        string root = FindProjectRoot();
        string clientSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerClient.cs"));

        Assert.Contains("public void UseBrokerPipe(string brokerPipeName)", clientSource);
        Assert.Contains("_usesExternalBroker = true;", clientSource);
        Assert.Contains("if (_usesExternalBroker)", clientSource);
        Assert.Contains("return true;", clientSource);
        Assert.Contains("if (_usesExternalBroker || _brokerProcess is { HasExited: false })", clientSource);
    }

    [Fact]
    public void HardwareMonitorReader_NarrowsCollectorsFromRequestedSensors()
    {
        string root = FindProjectRoot();
        string readerSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorReader.cs"));

        Assert.Contains("CollectorProfile.FromSensorIds(requestedSensorIds)", readerSource);
        Assert.Contains("return Full;", readerSource);
        Assert.Contains("id.StartsWith(\"/intelcpu/\"", readerSource);
        Assert.Contains("id.StartsWith(\"/nvidiagpu/\"", readerSource);
        Assert.Contains("id.StartsWith(\"/ram/\"", readerSource);
        Assert.Contains("id.StartsWith(\"/hdd/\"", readerSource);
        Assert.Contains("id.StartsWith(\"/lpc/\"", readerSource);
        Assert.Contains("requestedSensorIds is null || requestedSensorIds.Contains(reading.Id)", readerSource);
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
