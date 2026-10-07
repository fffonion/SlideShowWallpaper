using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class DesktopSessionRecoveryPolicyTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(15)]
    public void ShouldResumeDisplay_ConnectedOrDesktopReady_ReturnsTrue(int sessionEvent)
    {
        Assert.True(DesktopSessionRecoveryPolicy.ShouldResumeDisplay(sessionEvent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(9)]
    public void ShouldResumeDisplay_DisconnectedOrLocked_DoesNotClearPowerPause(int sessionEvent)
    {
        Assert.False(DesktopSessionRecoveryPolicy.ShouldResumeDisplay(sessionEvent));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(7, false)]
    public void ShouldRetryDisplay_SessionEvent_OnlyRetriesRemoteDisconnect(int sessionEvent, bool expected)
    {
        Assert.Equal(expected, DesktopSessionRecoveryPolicy.ShouldRetryDisplay(sessionEvent));
    }
}
