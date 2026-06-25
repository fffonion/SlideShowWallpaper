using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class UnelevatedRestartServiceTests
{
    [Fact]
    public void RestartIfCurrentProcessIsElevated_ReportsFailureInsteadOfContinuingElevatedMainProcess()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "UnelevatedRestartService.cs"));
        string method = source[
            source.IndexOf("public UnelevatedRestartResult RestartIfCurrentProcessIsElevated", StringComparison.Ordinal)..
            source.IndexOf("public static string BuildDemotedArguments", StringComparison.Ordinal)];

        Assert.Contains("return UnelevatedRestartResult.NotElevated;", method);
        Assert.Contains("return UnelevatedRestartResult.Failed;", method);
        Assert.Contains("return UnelevatedRestartResult.Restarted;", method);
        Assert.DoesNotContain("return false;", method);
    }

    [Fact]
    public void RestartIfCurrentProcessIsElevated_UsesShellTokenFallback()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "UnelevatedRestartService.cs"));

        Assert.Contains("TryStartWithLinkedToken(processPath, arguments)", source);
        Assert.Contains("TryStartWithShellToken(processPath, arguments)", source);
        Assert.Contains("NativeMethods.GetShellWindow()", source);
        Assert.Contains("NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION", source);
    }

    [Fact]
    public void RestartIfCurrentProcessIsElevated_StartsBrokerBeforeElevatedProcessExits()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "UnelevatedRestartService.cs"));
        string method = source[
            source.IndexOf("public UnelevatedRestartResult RestartIfCurrentProcessIsElevated", StringComparison.Ordinal)..
            source.IndexOf("public static string BuildDemotedArguments", StringComparison.Ordinal)];

        Assert.Contains("HardwareMonitorBrokerProtocol.CreatePipeName()", method);
        Assert.Contains("BuildDemotedArguments(arguments, brokerPipeName)", method);
        Assert.Contains("HardwareMonitorBrokerClient.StartBrokerProcess(", method);
        Assert.Contains("demotedProcess.ProcessId", method);
        Assert.Contains("requestElevation: false", method);
        Assert.Contains("demotedProcess.Resume()", method);
        Assert.Contains("demotedProcess.Terminate()", method);
    }

    [Fact]
    public void RestartIfCurrentProcessIsElevated_CreatesDemotedMainProcessSuspendedUntilBrokerStarts()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "UnelevatedRestartService.cs"));

        Assert.Contains("NativeMethods.CREATE_UNICODE_ENVIRONMENT | NativeMethods.CREATE_SUSPENDED", source);
        Assert.Contains("NativeMethods.ResumeThread(_threadHandle)", source);
        Assert.Contains("NativeMethods.TerminateProcess(_processHandle, 1)", source);
    }

    [Fact]
    public void BuildDemotedArguments_RemovesElevatedRestartAndAddsBrokerPipeAndNoDemote()
    {
        string arguments = UnelevatedRestartService.BuildDemotedArguments([
            "/q",
            AdministratorRestartService.RestartArgument,
            "/custom value",
        ], "pipe-name");

        Assert.Contains("\"/q\"", arguments);
        Assert.Contains("\"/custom value\"", arguments);
        Assert.Contains($"\"{LaunchOptions.HardwareBrokerPipeArgument}\" \"pipe-name\"", arguments);
        Assert.Contains($"\"{UnelevatedRestartService.NoDemoteArgument}\"", arguments);
        Assert.DoesNotContain(AdministratorRestartService.RestartArgument, arguments);
        Assert.DoesNotContain(LaunchOptions.ElevatedBrokerArgument, arguments);
    }

    [Fact]
    public void BuildDemotedArguments_DoesNotRequestBrokerElevationAfterMainProcessDemotion()
    {
        string arguments = UnelevatedRestartService.BuildDemotedArguments([
            "/q",
        ], "pipe-name");

        Assert.Contains($"\"{LaunchOptions.HardwareBrokerPipeArgument}\" \"pipe-name\"", arguments);
        Assert.Contains($"\"{UnelevatedRestartService.NoDemoteArgument}\"", arguments);
        Assert.DoesNotContain(LaunchOptions.ElevatedBrokerArgument, arguments);
    }

    [Fact]
    public void BuildDemotedArguments_DoesNotDuplicateNoDemoteAndAddsBrokerPipe()
    {
        string arguments = UnelevatedRestartService.BuildDemotedArguments([
            UnelevatedRestartService.NoDemoteArgument,
        ], "pipe-name");

        Assert.Equal(
            $"\"{LaunchOptions.HardwareBrokerPipeArgument}\" \"pipe-name\" \"{UnelevatedRestartService.NoDemoteArgument}\"",
            arguments);
    }

    [Fact]
    public void BuildDemotedArguments_DoesNotDuplicateExistingBrokerPipe()
    {
        string arguments = UnelevatedRestartService.BuildDemotedArguments([
            AdministratorRestartService.RestartArgument,
            LaunchOptions.HardwareBrokerPipeArgument,
            "old-pipe",
            LaunchOptions.ElevatedBrokerArgument,
        ], "new-pipe");

        Assert.Equal(
            $"\"{LaunchOptions.HardwareBrokerPipeArgument}\" \"new-pipe\" \"{UnelevatedRestartService.NoDemoteArgument}\"",
            arguments);
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
