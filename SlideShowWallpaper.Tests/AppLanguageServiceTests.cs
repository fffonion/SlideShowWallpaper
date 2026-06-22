using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class AppLanguageServiceTests
{
    [Fact]
    public void GetLanguageTag_WithSpecificLanguage_ReturnsBcp47Tag()
    {
        string languageTag = AppLanguageService.GetLanguageTag(AppLanguageMode.Japanese);

        Assert.Equal("ja-JP", languageTag);
    }

    [Fact]
    public void GetLanguageTag_WithSystemLanguage_ReturnsEmptyTag()
    {
        string languageTag = AppLanguageService.GetLanguageTag(AppLanguageMode.System);

        Assert.Equal(string.Empty, languageTag);
    }
}
