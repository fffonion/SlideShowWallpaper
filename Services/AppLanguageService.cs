using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class AppLanguageService
{
    private const string LanguageEnvironmentVariable = "SLIDESHOWWALLPAPER_LANGUAGE";

    public static void ApplyStartupLanguageOverride(SettingsStore settingsStore)
    {
        string? language = Environment.GetEnvironmentVariable(LanguageEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(language))
        {
            ApplyLanguageTag(language);
            return;
        }

        Apply(settingsStore.Load().LanguageMode);
    }

    public static void Apply(AppLanguageMode languageMode)
    {
        string? languageTag = GetLanguageTag(languageMode);
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return;
        }

        ApplyLanguageTag(languageTag);
    }

    private static void ApplyLanguageTag(string languageTag)
    {
        try
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = languageTag;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    internal static string? GetLanguageTag(AppLanguageMode languageMode)
    {
        return languageMode switch
        {
            AppLanguageMode.English => "en-US",
            AppLanguageMode.SimplifiedChinese => "zh-Hans",
            AppLanguageMode.TraditionalChinese => "zh-Hant",
            AppLanguageMode.Japanese => "ja-JP",
            _ => null,
        };
    }
}
