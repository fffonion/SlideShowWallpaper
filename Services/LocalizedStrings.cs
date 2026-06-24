using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace SlideShowWallpaper.Services;

public static class LocalizedStrings
{
    private static ResourceLoader _loader = new();

    public static string Get(string key)
    {
        try
        {
            string value = _loader.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch
        {
            return key;
        }
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    public static void Reset()
    {
        _loader = new ResourceLoader();
    }
}
