using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Tests;

public sealed class TimeUnitConverterTests
{
    [Theory]
    [InlineData(2, TimeUnit.Seconds, 2)]
    [InlineData(2, TimeUnit.Minutes, 120)]
    [InlineData(2, TimeUnit.Hours, 7200)]
    public void ToSeconds_WithUnit_ReturnsSeconds(double value, TimeUnit unit, double expected)
    {
        Assert.Equal(expected, TimeUnitConverter.ToSeconds(value, unit));
    }

    [Theory]
    [InlineData(0.8, TimeUnit.Seconds, 800)]
    [InlineData(2, TimeUnit.Minutes, 120000)]
    [InlineData(1, TimeUnit.Hours, 3600000)]
    public void ToMilliseconds_WithUnit_ReturnsMilliseconds(double value, TimeUnit unit, int expected)
    {
        Assert.Equal(expected, TimeUnitConverter.ToMilliseconds(value, unit));
    }
}
