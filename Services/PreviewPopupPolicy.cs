using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

internal static class PreviewPopupPolicy
{
    public static TimeSpan HoverDelay { get; } = TimeSpan.FromSeconds(5);

    public static bool ShouldMuteVideo(bool globalMute, MonitorProfile profile)
    {
        return globalMute || !profile.VideoSoundEnabled;
    }
}
