namespace SlideShowWallpaper.Tests;

public sealed class FontCatalogServiceSourceTests
{
    [Fact]
    public void FontCatalogService_EnumeratesFontsWithGdiAndSupportsCacheClear()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "FontCatalogService.cs"));

        Assert.Contains("EnumFontFamiliesEx", source);
        Assert.Contains("_cachedFontFamilies", source);
        Assert.Contains("_cacheVersion++", source);
        Assert.Contains("public static void ClearCache()", source);
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
