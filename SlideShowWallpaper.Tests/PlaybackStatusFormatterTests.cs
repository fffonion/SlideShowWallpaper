using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class PlaybackStatusFormatterTests
{
    [Fact]
    public void FormatCurrentIndex_WithNoItems_ReturnsZeroOfZero()
    {
        Assert.Equal("0/0", PlaybackStatusFormatter.FormatCurrentIndex(0, 0, "{0}/{1}"));
    }

    [Fact]
    public void FormatLoopRemaining_RoundsUpToMinutes()
    {
        Assert.Equal("Remaining 1h 02m", PlaybackStatusFormatter.FormatLoopRemaining(3661, "Remaining {0}"));
    }

    [Fact]
    public void FormatLoopRemaining_WithLessThanOneHour_UsesMinutesOnly()
    {
        Assert.Equal("Remaining 4m", PlaybackStatusFormatter.FormatLoopRemaining(240, "Remaining {0}"));
    }

    [Fact]
    public void FormatPreviewStatus_WithItems_IncludesIndexCountAndRemaining()
    {
        string result = PlaybackStatusFormatter.FormatPreviewStatus(2, 5, 240, "{0}/{1} media · {2}", "0 media", "Remaining {0}", "{0}h {1:00}m");

        Assert.Equal("2/5 media · Remaining 4m", result);
    }

    [Fact]
    public void FormatPreviewStatus_WithNoItems_ReturnsZeroText()
    {
        string result = PlaybackStatusFormatter.FormatPreviewStatus(0, 0, 240, "{0}/{1} media · {2}", "0 media", "Remaining {0}", "{0}h {1:00}m");

        Assert.Equal("0 media", result);
    }

    [Fact]
    public void FormatPreviewStatusWithoutRemaining_WithItems_ExcludesRemainingText()
    {
        string result = PlaybackStatusFormatter.FormatPreviewStatusWithoutRemaining(2, 5, "{0}/{1} media", "0 media");

        Assert.Equal("2/5 media", result);
    }

    [Fact]
    public void FormatPreviewStatusWithoutRemaining_WithNoItems_ReturnsZeroText()
    {
        string result = PlaybackStatusFormatter.FormatPreviewStatusWithoutRemaining(0, 0, "{0}/{1} media", "0 media");

        Assert.Equal("0 media", result);
    }

    [Fact]
    public void CalculateLoopRemainingSeconds_IncludesFullLoopFromCurrentItem()
    {
        var startedAt = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);
        var now = startedAt.AddSeconds(10);

        int remaining = PlaybackStatusFormatter.CalculateLoopRemainingSeconds(2, 5, 60, startedAt, now);

        Assert.Equal(290, remaining);
    }

    [Fact]
    public void CalculateLoopRemainingSeconds_WithSingleLoop_IncludesCurrentItemOnly()
    {
        var startedAt = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);
        var now = startedAt.AddSeconds(10);

        int remaining = PlaybackStatusFormatter.CalculateLoopRemainingSeconds(2, 5, 60, startedAt, now, PlaybackOrder.SingleLoop);

        Assert.Equal(50, remaining);
    }

    [Fact]
    public void CalculateLoopRemainingSeconds_WithLoadedQueueAndNoStartTime_IncludesFullLoop()
    {
        var now = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);

        int remaining = PlaybackStatusFormatter.CalculateLoopRemainingSeconds(0, 4, 60, null, now);

        Assert.Equal(240, remaining);
    }

    [Fact]
    public void CalculateLoopRemainingSeconds_WithSingleLoopAndNoStartTime_IncludesOneInterval()
    {
        var now = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);

        int remaining = PlaybackStatusFormatter.CalculateLoopRemainingSeconds(0, 4, 60, null, now, PlaybackOrder.SingleLoop);

        Assert.Equal(60, remaining);
    }
}
