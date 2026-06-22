using System.Globalization;

namespace SlideShowWallpaper.Services;

public static class PlaybackStatusFormatter
{
    public static string FormatCurrentIndex(int currentIndex, int totalCount)
    {
        return FormatCurrentIndex(currentIndex, totalCount, LocalizedStrings.Get("CurrentIndexFormat"));
    }

    public static string FormatCurrentIndex(int currentIndex, int totalCount, string format)
    {
        int current = totalCount <= 0 ? 0 : Math.Clamp(currentIndex, 1, totalCount);
        int total = Math.Max(0, totalCount);
        return string.Format(CultureInfo.CurrentCulture, format, current, total);
    }

    public static string FormatLoopRemaining(int remainingSeconds)
    {
        return FormatLoopRemaining(remainingSeconds, LocalizedStrings.Get("LoopRemainingFormat"), LocalizedStrings.Get("LoopRemainingTimeFormat"));
    }

    public static string FormatLoopRemaining(int remainingSeconds, string format)
    {
        return FormatLoopRemaining(remainingSeconds, format, "{0}h {1:00}m");
    }

    public static string FormatLoopRemaining(int remainingSeconds, string format, string timeFormat)
    {
        int totalMinutes = Math.Max(0, (int)Math.Ceiling(remainingSeconds / 60.0));
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        string value = string.Format(CultureInfo.CurrentCulture, timeFormat, hours, minutes);
        return string.Format(CultureInfo.CurrentCulture, format, value);
    }

    public static int CalculateLoopRemainingSeconds(
        int currentIndex,
        int totalCount,
        int intervalSeconds,
        DateTimeOffset? currentStartedAt,
        DateTimeOffset now)
    {
        if (currentIndex <= 0 || totalCount <= 0 || currentStartedAt is null)
        {
            return 0;
        }

        int interval = Math.Max(0, intervalSeconds);
        double elapsed = Math.Max(0, (now - currentStartedAt.Value).TotalSeconds);
        int currentRemaining = Math.Max(0, (int)Math.Ceiling(interval - elapsed));
        int remainingItems = Math.Max(0, totalCount - Math.Clamp(currentIndex, 1, totalCount));
        return currentRemaining + (remainingItems * interval);
    }
}
