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
    public void CalculateLoopRemainingSeconds_IncludesCurrentIntervalAndRemainingItems()
    {
        var startedAt = new DateTimeOffset(2026, 6, 22, 12, 0, 0, TimeSpan.Zero);
        var now = startedAt.AddSeconds(10);

        int remaining = PlaybackStatusFormatter.CalculateLoopRemainingSeconds(2, 5, 60, startedAt, now);

        Assert.Equal(230, remaining);
    }
}
