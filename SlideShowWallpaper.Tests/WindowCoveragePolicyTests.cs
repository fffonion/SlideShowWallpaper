using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class WindowCoveragePolicyTests
{
    [Fact]
    public void ShouldPauseVideo_WithForegroundFromCurrentProcess_ReturnsFalse()
    {
        var foreground = new ForegroundWindowInfo(
            new IntPtr(1),
            Environment.ProcessId,
            IsMaximized: true,
            new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 });
        var monitor = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

        bool result = WindowCoveragePolicy.ShouldPauseVideo(foreground, monitor, Environment.ProcessId);

        Assert.False(result);
    }

    [Fact]
    public void ShouldPauseVideo_WithMaximizedForegroundOnMonitor_ReturnsTrue()
    {
        var foreground = new ForegroundWindowInfo(
            new IntPtr(1),
            Environment.ProcessId + 1,
            IsMaximized: true,
            new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 });
        var monitor = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

        bool result = WindowCoveragePolicy.ShouldPauseVideo(foreground, monitor, Environment.ProcessId);

        Assert.True(result);
    }

    [Fact]
    public void ShouldPauseVideo_WithFullscreenForegroundCoveringMonitor_ReturnsTrue()
    {
        var foreground = new ForegroundWindowInfo(
            new IntPtr(1),
            Environment.ProcessId + 1,
            IsMaximized: false,
            new NativeMethods.RECT { Left = 1920, Top = 0, Right = 3840, Bottom = 1080 });
        var monitor = new NativeMethods.RECT { Left = 1920, Top = 0, Right = 3840, Bottom = 1080 };

        bool result = WindowCoveragePolicy.ShouldPauseVideo(foreground, monitor, Environment.ProcessId);

        Assert.True(result);
    }

    [Fact]
    public void ShouldPauseVideo_WithNormalForeground_ReturnsFalse()
    {
        var foreground = new ForegroundWindowInfo(
            new IntPtr(1),
            Environment.ProcessId + 1,
            IsMaximized: false,
            new NativeMethods.RECT { Left = 200, Top = 200, Right = 1200, Bottom = 900 });
        var monitor = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

        bool result = WindowCoveragePolicy.ShouldPauseVideo(foreground, monitor, Environment.ProcessId);

        Assert.False(result);
    }

    [Fact]
    public void ShouldPauseVideo_WithDisplayPowerPauseEnabled_ReturnsTrue()
    {
        var monitor = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

        bool result = WindowCoveragePolicy.ShouldPauseVideo(
            foregroundWindow: null,
            monitor,
            Environment.ProcessId,
            pauseWhenDisplayOffOrSleeping: true,
            isDisplayOffOrSleeping: true);

        Assert.True(result);
    }

    [Fact]
    public void ShouldPauseVideo_WithDisplayPowerPauseDisabled_ReturnsFalse()
    {
        var monitor = new NativeMethods.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 };

        bool result = WindowCoveragePolicy.ShouldPauseVideo(
            foregroundWindow: null,
            monitor,
            Environment.ProcessId,
            pauseWhenDisplayOffOrSleeping: false,
            isDisplayOffOrSleeping: true);

        Assert.False(result);
    }
}
