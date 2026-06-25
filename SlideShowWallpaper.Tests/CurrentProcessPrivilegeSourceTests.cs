namespace SlideShowWallpaper.Tests;

public sealed class CurrentProcessPrivilegeSourceTests
{
    [Fact]
    public void IsElevated_UsesTokenElevationForTaskManagerEquivalentState()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "CurrentProcessPrivilege.cs"));

        Assert.Contains("public static bool IsElevated()", source);
        Assert.Contains("ProcessPrivilegeNativeMethods.TokenElevation", source);
        Assert.Contains("ProcessPrivilegeNativeMethods.TOKEN_ELEVATION", source);
        Assert.Contains("elevation.TokenIsElevated != 0", source);
        Assert.DoesNotContain("WindowsPrincipal", source);
        Assert.DoesNotContain("IsAdministrator", source);
    }

    [Fact]
    public void HardwareBrokerProject_IncludesOnlySmallPrivilegeNativeHelper()
    {
        string root = FindProjectRoot();
        string project = File.ReadAllText(Path.Combine(root, "HardwareBroker", "SlideShowWallpaper.HardwareBroker.csproj"));

        Assert.Contains("ProcessPrivilegeNativeMethods.cs", project);
        Assert.DoesNotContain("Interop\\NativeMethods.cs", project);
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
