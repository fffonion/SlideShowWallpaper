using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class HardwareOverlayTemplateServiceTests
{
    [Fact]
    public void FromConfig_IncludesBackgroundColor()
    {
        var config = new HardwareMonitorConfig
        {
            BackgroundImagePath = @"C:\Wallpapers\panel.png",
            BackgroundColor = "#80223344",
        };

        HardwareOverlayTemplate template = HardwareOverlayTemplateService.FromConfig(config);

        Assert.Equal(@"C:\Wallpapers\panel.png", template.BackgroundImagePath);
        Assert.Equal("#80223344", template.BackgroundColor);
    }

    [Fact]
    public void ApplyToConfig_IncludesBackgroundColor()
    {
        var config = new HardwareMonitorConfig();
        var template = new HardwareOverlayTemplate
        {
            BackgroundImagePath = @"C:\Wallpapers\panel.png",
            BackgroundColor = "#80223344",
        };

        HardwareOverlayTemplateService.ApplyToConfig(template, config);

        Assert.Equal(@"C:\Wallpapers\panel.png", config.BackgroundImagePath);
        Assert.Equal("#80223344", config.BackgroundColor);
    }

    [Fact]
    public void FromConfig_CopiesElementDecimalPlaces()
    {
        var config = new HardwareMonitorConfig
        {
            Elements =
            [
                new HardwareOverlayElement
                {
                    Kind = HardwareOverlayElementKind.Sensor,
                    DecimalPlaces = 3,
                },
            ],
        };

        HardwareOverlayTemplate template = HardwareOverlayTemplateService.FromConfig(config);

        HardwareOverlayElement element = Assert.Single(template.Elements);
        Assert.Equal(3, element.DecimalPlaces);
    }

    [Fact]
    public void ApplyToConfig_CopiesElementDecimalPlaces()
    {
        var config = new HardwareMonitorConfig();
        var template = new HardwareOverlayTemplate
        {
            Elements =
            [
                new HardwareOverlayElement
                {
                    Kind = HardwareOverlayElementKind.Sensor,
                    DecimalPlaces = 4,
                },
            ],
        };

        HardwareOverlayTemplateService.ApplyToConfig(template, config);

        HardwareOverlayElement element = Assert.Single(config.Elements);
        Assert.Equal(4, element.DecimalPlaces);
    }
}
