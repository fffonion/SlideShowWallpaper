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
}
