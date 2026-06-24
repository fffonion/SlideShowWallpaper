using System.Xml.Linq;

namespace SlideShowWallpaper.Tests;

public sealed class ProjectPackagingTests
{
    [Fact]
    public void ProjectTargetsNet10()
    {
        string projectPath = FindProjectPath();
        XDocument project = XDocument.Load(projectPath);
        string? targetFramework = project
            .Descendants("TargetFramework")
            .Select(element => element.Value)
            .FirstOrDefault();

        Assert.Equal("net10.0-windows10.0.26100.0", targetFramework);
    }

    [Fact]
    public void ProjectReferencesWinUiPackageWithoutFullWindowsAppSdk()
    {
        string projectPath = FindProjectPath();
        XDocument project = XDocument.Load(projectPath);
        string[] packageReferences = project
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        Assert.Contains("Microsoft.WindowsAppSDK.WinUI", packageReferences);
        Assert.DoesNotContain("Microsoft.WindowsAppSDK", packageReferences);
        Assert.DoesNotContain("LibreHardwareMonitorLib", packageReferences);
    }

    [Fact]
    public void ProjectExcludesBrokerOnlyHardwareReaderFromMainAssembly()
    {
        string projectPath = FindProjectPath();
        XDocument project = XDocument.Load(projectPath);
        string[] removedCompileItems = project
            .Descendants("Compile")
            .Select(element => element.Attribute("Remove")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        Assert.Contains(@"Services\HardwareMonitorBrokerHost.cs", removedCompileItems);
        Assert.Contains(@"Services\HardwareMonitorReader.cs", removedCompileItems);
    }

    [Fact]
    public void ProjectExcludesArtifactsFromPublishedContent()
    {
        string projectPath = FindProjectPath();
        XDocument project = XDocument.Load(projectPath);
        string[] removedContent = project
            .Descendants()
            .Where(element => element.Name.LocalName is "Content" or "None")
            .Select(element => element.Attribute("Remove")?.Value)
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();

        Assert.Contains(@"artifacts\**\*", removedContent);
    }

    private static string FindProjectPath()
    {
        string directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            string projectPath = Path.Combine(directory, "SlideShowWallpaper.csproj");
            if (File.Exists(projectPath))
            {
                return projectPath;
            }

            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }

        throw new FileNotFoundException("SlideShowWallpaper.csproj was not found.");
    }
}
