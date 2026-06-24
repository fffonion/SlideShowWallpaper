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
    public void FromConfig_IncludesOverlaySize()
    {
        var config = new HardwareMonitorConfig
        {
            OverlayWidth = 512,
            OverlayHeight = 288,
        };

        HardwareOverlayTemplate template = HardwareOverlayTemplateService.FromConfig(config);

        Assert.Equal(512, template.OverlayWidth);
        Assert.Equal(288, template.OverlayHeight);
    }

    [Fact]
    public void FromConfig_WithFractionalLayout_UsesWholePixelLayoutValues()
    {
        var config = new HardwareMonitorConfig
        {
            X = 10.5,
            Y = 20.4,
            OverlayWidth = 320.5,
            OverlayHeight = 180.4,
            Elements =
            [
                new HardwareOverlayElement
                {
                    X = 1.5,
                    Y = 2.4,
                    Width = 160.5,
                    Height = 40.4,
                },
            ],
        };

        HardwareOverlayTemplate template = HardwareOverlayTemplateService.FromConfig(config);

        Assert.Equal(11, template.X);
        Assert.Equal(20, template.Y);
        Assert.Equal(321, template.OverlayWidth);
        Assert.Equal(180, template.OverlayHeight);
        HardwareOverlayElement element = Assert.Single(template.Elements);
        Assert.Equal(2, element.X);
        Assert.Equal(2, element.Y);
        Assert.Equal(161, element.Width);
        Assert.Equal(40, element.Height);
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
    public void ApplyToConfig_IncludesOverlaySize()
    {
        var config = new HardwareMonitorConfig();
        var template = new HardwareOverlayTemplate
        {
            OverlayWidth = 640,
            OverlayHeight = 360,
        };

        HardwareOverlayTemplateService.ApplyToConfig(template, config);

        Assert.Equal(640, config.OverlayWidth);
        Assert.Equal(360, config.OverlayHeight);
    }

    [Fact]
    public void ApplyToConfig_WithFractionalLayout_UsesWholePixelLayoutValues()
    {
        var config = new HardwareMonitorConfig();
        var template = new HardwareOverlayTemplate
        {
            X = 10.5,
            Y = 20.4,
            OverlayWidth = 320.5,
            OverlayHeight = 180.4,
            Elements =
            [
                new HardwareOverlayElement
                {
                    X = 1.5,
                    Y = 2.4,
                    Width = 160.5,
                    Height = 40.4,
                },
            ],
        };

        HardwareOverlayTemplateService.ApplyToConfig(template, config);

        Assert.Equal(11, config.X);
        Assert.Equal(20, config.Y);
        Assert.Equal(321, config.OverlayWidth);
        Assert.Equal(180, config.OverlayHeight);
        HardwareOverlayElement element = Assert.Single(config.Elements);
        Assert.Equal(2, element.X);
        Assert.Equal(2, element.Y);
        Assert.Equal(161, element.Width);
        Assert.Equal(40, element.Height);
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
