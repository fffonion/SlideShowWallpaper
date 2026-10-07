using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class DisplayMediaRecoveryStateTests
{
    [Fact]
    public void Take_PhysicalMonitorReturnsAfterRemoteDisplay_RecoversOnce()
    {
        var recovery = new DisplayMediaRecoveryState();
        recovery.Request(["physical-a", "physical-b"]);

        Assert.False(recovery.Take("physical-a", isMonitorAvailable: false));
        Assert.False(recovery.Take("remote-display", isMonitorAvailable: true));
        Assert.False(recovery.Take("physical-a", isMonitorAvailable: false));
        Assert.True(recovery.Take("physical-a", isMonitorAvailable: true));
        Assert.False(recovery.Take("physical-a", isMonitorAvailable: true));
        Assert.True(recovery.Take("physical-b", isMonitorAvailable: true));
    }

    [Fact]
    public void Request_DuplicateSignals_CollapsesPendingRecovery()
    {
        var recovery = new DisplayMediaRecoveryState();
        recovery.Request(["physical-a"]);
        recovery.Request(["PHYSICAL-A"]);

        Assert.True(recovery.Take("physical-a", isMonitorAvailable: true));
        Assert.False(recovery.Take("physical-a", isMonitorAvailable: true));
    }

    [Fact]
    public void Clear_PlaybackStops_DiscardsPendingRecovery()
    {
        var recovery = new DisplayMediaRecoveryState();
        recovery.Request(["physical-a"]);

        recovery.Clear();

        Assert.False(recovery.Take("physical-a", isMonitorAvailable: true));
    }

    [Fact]
    public void Request_EmptyProfiles_DoesNotRecoverUnrequestedMonitor()
    {
        var recovery = new DisplayMediaRecoveryState();
        recovery.Request([]);

        Assert.False(recovery.Take("physical-a", isMonitorAvailable: true));
    }
}
