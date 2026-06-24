namespace SlideShowWallpaper.Tests;

public sealed class ReleaseWorkflowTests
{
    [Fact]
    public void ReleaseWorkflow_StampsSingleFileWithReleaseTagVersion()
    {
        string root = FindProjectRoot();
        string workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "release.yml"));

        int resolveIndex = workflow.IndexOf("- name: Resolve release metadata", StringComparison.Ordinal);
        int buildIndex = workflow.IndexOf("- name: Build single-file executable", StringComparison.Ordinal);

        Assert.True(resolveIndex >= 0);
        Assert.True(buildIndex > resolveIndex);
        Assert.Contains("RELEASE_VERSION=$numericVersion", workflow);
        Assert.Contains("RELEASE_FILE_VERSION=$fileVersion", workflow);
        Assert.Contains("-p:Version=$env:RELEASE_VERSION", workflow);
        Assert.Contains("-p:InformationalVersion=$env:RELEASE_TAG", workflow);
    }

    [Fact]
    public void PackageManifest_DeclaresInternetClientForUpdateChecks()
    {
        string root = FindProjectRoot();
        string manifest = File.ReadAllText(Path.Combine(root, "Package.appxmanifest"));

        Assert.Contains("<Capability Name=\"internetClient\" />", manifest);
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
