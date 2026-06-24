using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class UnelevatedRestartServiceTests
{
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
        Assert.Contains($"\"{UnelevatedRestartService.NoDemoteArgument}\"", arguments);
        Assert.DoesNotContain(AdministratorRestartService.RestartArgument, arguments);
    }

    [Fact]
    public void BuildDemotedArguments_DoesNotDuplicateNoDemote()
    {
        string arguments = UnelevatedRestartService.BuildDemotedArguments([
            UnelevatedRestartService.NoDemoteArgument,
        ]);

        Assert.Equal($"\"{UnelevatedRestartService.NoDemoteArgument}\"", arguments);
    }
}
