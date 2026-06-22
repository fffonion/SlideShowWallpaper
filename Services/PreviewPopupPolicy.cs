using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

internal static class PreviewPopupPolicy
{
    public const int MinimumHoverDelaySeconds = 1;

    public static TimeSpan GetHoverDelay(int seconds)
    {
        return TimeSpan.FromSeconds(Math.Max(MinimumHoverDelaySeconds, seconds));
    }

    public static bool ShouldMuteVideo(bool globalMute, MonitorProfile profile)
    {
        return globalMute || !profile.VideoSoundEnabled;
    }
}
