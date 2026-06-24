using System.Xml.Linq;

namespace SlideShowWallpaper.Tests;

public sealed class LocalizationResourceTests
{
    [Fact]
    public void Resources_AllCultures_UseSameKeys()
    {
        string root = FindRepositoryRoot();
        string[] cultures = ["en-US", "zh-Hans", "zh-Hant", "ja-JP"];
        Dictionary<string, string[]> keysByCulture = cultures.ToDictionary(
            culture => culture,
            culture => ReadKeys(Path.Combine(root, "Strings", culture, "Resources.resw")));

        string[] expected = keysByCulture["en-US"];
        foreach ((string culture, string[] keys) in keysByCulture)
        {
            Assert.Empty(expected.Except(keys));
            Assert.Empty(keys.Except(expected));
        }
    }

    [Fact]
    public void Resources_IncludeHardwareMonitorWindowTitle()
    {
        string root = FindRepositoryRoot();
        string[] keys = ReadKeys(Path.Combine(root, "Strings", "en-US", "Resources.resw"));

        Assert.Contains("HardwareMonitorTitle", keys);
    }

    [Fact]
    public void LocalizedStrings_GetFallsBackToKeyWhenResourceLoaderThrows()
    {
        string root = FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "LocalizedStrings.cs"));
        string method = source[source.IndexOf("public static string Get", StringComparison.Ordinal)..source.IndexOf("public static string Format", StringComparison.Ordinal)];

        Assert.Contains("catch", method);
        Assert.Contains("return key;", method);
    }

    private static string[] ReadKeys(string path)
    {
        XDocument document = XDocument.Load(path);
        return document.Root?
            .Elements("data")
            .Select(element => element.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Order(StringComparer.Ordinal)
            .ToArray() ?? [];
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SlideShowWallpaper.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
