using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class MonitorDisplayNameTests
{
    [Fact]
    public void BuildDisplayName_uses_friendly_name_when_available()
    {
        string name = MonitorService.BuildDisplayName(@"\\.\DISPLAY1", "Dell U2723QE");

        Assert.Equal("Dell U2723QE", name);
    }

    [Fact]
    public void BuildDisplayName_falls_back_to_display_label_without_friendly_name()
    {
        string name = MonitorService.BuildDisplayName(@"\\.\DISPLAY2", "");

        Assert.Equal("Display 2", name);
    }
}
