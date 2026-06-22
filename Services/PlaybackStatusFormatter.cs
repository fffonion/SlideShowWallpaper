using System.Globalization;
using SlideShowWallpaper.Models;

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

    public static string FormatPreviewStatus(int currentIndex, int totalCount, int remainingSeconds)
    {
        return FormatPreviewStatus(
            currentIndex,
            totalCount,
            remainingSeconds,
            LocalizedStrings.Get("PreviewStatusFormat"),
            LocalizedStrings.Get("ImageCountZero"),
            LocalizedStrings.Get("LoopRemainingFormat"),
            LocalizedStrings.Get("LoopRemainingTimeFormat"));
    }

    public static string FormatPreviewStatusWithoutRemaining(int currentIndex, int totalCount)
    {
        return FormatPreviewStatusWithoutRemaining(
            currentIndex,
            totalCount,
            LocalizedStrings.Get("PreviewStatusNoRemainingFormat"),
            LocalizedStrings.Get("ImageCountZero"));
    }

    public static string FormatPreviewStatusWithoutRemaining(int currentIndex, int totalCount, string format, string zeroText)
    {
        if (totalCount <= 0)
        {
            return zeroText;
        }

        int current = Math.Clamp(currentIndex, 1, totalCount);
        return string.Format(CultureInfo.CurrentCulture, format, current, totalCount);
    }

    public static string FormatPreviewStatus(
        int currentIndex,
        int totalCount,
        int remainingSeconds,
        string format,
        string zeroText,
        string remainingFormat,
        string timeFormat)
    {
        if (totalCount <= 0)
        {
            return zeroText;
        }

        int current = Math.Clamp(currentIndex, 1, totalCount);
        string remaining = FormatLoopRemaining(remainingSeconds, remainingFormat, timeFormat);
        return string.Format(CultureInfo.CurrentCulture, format, current, totalCount, remaining);
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
        string value = hours == 0
            ? string.Format(CultureInfo.CurrentCulture, "{0}m", minutes)
            : string.Format(CultureInfo.CurrentCulture, timeFormat, hours, minutes);
        return string.Format(CultureInfo.CurrentCulture, format, value);
    }

    public static int CalculateLoopRemainingSeconds(
        int currentIndex,
        int totalCount,
        int intervalSeconds,
        DateTimeOffset? currentStartedAt,
        DateTimeOffset now,
        PlaybackOrder playbackOrder = PlaybackOrder.SequentialLoop)
    {
        if (totalCount <= 0)
        {
            return 0;
        }

        int interval = Math.Max(0, intervalSeconds);
        double elapsed = currentStartedAt is null ? 0 : Math.Max(0, (now - currentStartedAt.Value).TotalSeconds);
        int currentRemaining = Math.Max(0, (int)Math.Ceiling(interval - elapsed));
        int loopItemCount = playbackOrder == PlaybackOrder.SingleLoop ? 1 : totalCount;
        int remainingItems = Math.Max(0, loopItemCount - 1);
        return currentRemaining + (remainingItems * interval);
    }
}
