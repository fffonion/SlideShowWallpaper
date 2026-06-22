using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class VideoPlaybackPolicyTests
{
    [Fact]
    public void ShouldLoopVideo_WithSingleLoopOrder_ReturnsTrue()
    {
        var profile = new MonitorProfile
        {
            PlaybackOrder = PlaybackOrder.SingleLoop,
            VideoLoop = false,
        };

        bool result = VideoPlaybackPolicy.ShouldLoopVideo(profile);

        Assert.True(result);
    }

    [Fact]
    public void ShouldLoopVideo_WithNormalOrder_UsesVideoLoopSetting()
    {
        var profile = new MonitorProfile
        {
            PlaybackOrder = PlaybackOrder.NameAsc,
            VideoLoop = false,
        };

        bool result = VideoPlaybackPolicy.ShouldLoopVideo(profile);

        Assert.False(result);
    }
}
