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
        Assert.Contains("return started ? UnelevatedRestartResult.Restarted : UnelevatedRestartResult.Failed;", method);
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
    public void BuildDemotedArguments_RemovesElevatedRestartAndAddsNoDemote()
    {
        string arguments = UnelevatedRestartService.BuildDemotedArguments([
            "/q",
            AdministratorRestartService.RestartArgument,
            "/custom value",
        ]);

        Assert.Contains("\"/q\"", arguments);
        Assert.Contains("\"/custom value\"", arguments);
        Assert.Contains($"\"{LaunchOptions.ElevatedBrokerArgument}\"", arguments);
        Assert.Contains($"\"{UnelevatedRestartService.NoDemoteArgument}\"", arguments);
        Assert.DoesNotContain(AdministratorRestartService.RestartArgument, arguments);
    }

    [Fact]
    public void BuildDemotedArguments_AlwaysRequestsElevatedBrokerAfterMainProcessDemotion()
    {
        string arguments = UnelevatedRestartService.BuildDemotedArguments([
            "/q",
        ]);

        Assert.Contains($"\"{LaunchOptions.ElevatedBrokerArgument}\"", arguments);
        Assert.Contains($"\"{UnelevatedRestartService.NoDemoteArgument}\"", arguments);
    }

    [Fact]
    public void BuildDemotedArguments_DoesNotDuplicateNoDemoteAndStillRequestsElevatedBroker()
    {
        string arguments = UnelevatedRestartService.BuildDemotedArguments([
            UnelevatedRestartService.NoDemoteArgument,
        ]);

        Assert.Equal(
            $"\"{LaunchOptions.ElevatedBrokerArgument}\" \"{UnelevatedRestartService.NoDemoteArgument}\"",
            arguments);
    }

    [Fact]
    public void BuildDemotedArguments_DoesNotDuplicateElevatedBroker()
    {
        string arguments = UnelevatedRestartService.BuildDemotedArguments([
            AdministratorRestartService.RestartArgument,
            LaunchOptions.ElevatedBrokerArgument,
        ]);

        Assert.Equal(
            $"\"{LaunchOptions.ElevatedBrokerArgument}\" \"{UnelevatedRestartService.NoDemoteArgument}\"",
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
