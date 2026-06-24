using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Tests;

public sealed class TimeUnitConverterTests
{
    [Theory]
    [InlineData(2, (int)TimeUnit.Seconds, 2)]
    [InlineData(2, (int)TimeUnit.Minutes, 120)]
    [InlineData(2, (int)TimeUnit.Hours, 7200)]
    public void ToSeconds_WithUnit_ReturnsSeconds(double value, int unitValue, double expected)
    {
        var unit = (TimeUnit)unitValue;

        Assert.Equal(expected, TimeUnitConverter.ToSeconds(value, unit));
    }

    [Theory]
    [InlineData(0.8, (int)TimeUnit.Seconds, 800)]
    [InlineData(2, (int)TimeUnit.Minutes, 120000)]
    [InlineData(1, (int)TimeUnit.Hours, 3600000)]
    public void ToMilliseconds_WithUnit_ReturnsMilliseconds(double value, int unitValue, int expected)
    {
        var unit = (TimeUnit)unitValue;

        Assert.Equal(expected, TimeUnitConverter.ToMilliseconds(value, unit));
    }
}
