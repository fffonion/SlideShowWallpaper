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
        Assert.Contains("-p:IncludeSourceRevisionInInformationalVersion=false", workflow);
    }

    [Fact]
    public void Project_StampsLocalBuildVersionFromGitTag()
    {
        string root = FindProjectRoot();
        string project = File.ReadAllText(Path.Combine(root, "SlideShowWallpaper.csproj"));

        Assert.Contains("ResolveGitTagVersion", project);
        Assert.Contains("BeforeTargets=\"GetAssemblyVersion;GenerateAssemblyInfo\"", project);
        Assert.Contains("git describe --tags --abbrev=0 --match v[0-9]*", project);
        Assert.Contains("<Version>$(GitReleaseVersion)</Version>", project);
        Assert.Contains("<AssemblyVersion>$(GitReleaseFileVersion)</AssemblyVersion>", project);
        Assert.Contains("<FileVersion>$(GitReleaseFileVersion)</FileVersion>", project);
        Assert.Contains("<InformationalVersion>$(GitReleaseTag)</InformationalVersion>", project);
        Assert.Contains("<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>", project);
        Assert.Contains("DisableGitVersionStamp", project);
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
