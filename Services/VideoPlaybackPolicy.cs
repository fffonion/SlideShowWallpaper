using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class VideoPlaybackPolicy
{
    public static bool ShouldLoopVideo(MonitorProfile profile)
    {
        return profile.PlaybackOrder == PlaybackOrder.SingleLoop || profile.VideoLoop;
    }
}
