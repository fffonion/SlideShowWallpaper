using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class PreviewPopupPolicyTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    public void ShouldMuteVideo_WithGlobalAndProfileSettings_ReturnsExpectedValue(bool globalMute, bool videoSoundEnabled, bool expected)
    {
        var profile = new MonitorProfile
        {
            VideoSoundEnabled = videoSoundEnabled,
        };

        bool result = PreviewPopupPolicy.ShouldMuteVideo(globalMute, profile);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void HoverDelay_ReturnsFiveSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), PreviewPopupPolicy.HoverDelay);
    }
}
