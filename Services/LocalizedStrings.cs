using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace SlideShowWallpaper.Services;

public static class LocalizedStrings
{
    private static readonly Lazy<ResourceLoader> Loader = new(() => new ResourceLoader());

    public static string Get(string key)
    {
        string value = Loader.Value.GetString(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }
}
