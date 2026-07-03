using SlideShowWallpaper.Models;
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
    public void BuildDisplayName_RemovesTrailingParenthesizedDescription()
    {
        string name = MonitorService.BuildDisplayName(@"\\.\DISPLAY2", "Philips 24E1N5500(23.8 inch Wide LCD MONITOR 24E1N5500)");

        Assert.Equal("Philips 24E1N5500", name);
    }

    [Fact]
    public void BuildDisplayName_falls_back_to_display_label_without_friendly_name()
    {
        string name = MonitorService.BuildDisplayName(@"\\.\DISPLAY2", "");

        Assert.Equal("Display 2", name);
    }

    [Fact]
    public void SortBySystemDisplayNumber_UsesWindowsDisplayNumberOrder()
    {
        MonitorProfile[] monitors =
        [
            new() { Id = @"\\.\DISPLAY10", DisplayName = "Display 10" },
            new() { Id = @"\\.\DISPLAY2", DisplayName = "Display 2" },
            new() { Id = "VirtualMonitor", DisplayName = "Virtual" },
            new() { Id = @"\\.\DISPLAY1", DisplayName = "Display 1" },
        ];

        IReadOnlyList<MonitorProfile> sorted = MonitorService.SortBySystemDisplayNumber(monitors);

        Assert.Collection(
            sorted,
            monitor => Assert.Equal(@"\\.\DISPLAY1", monitor.Id),
            monitor => Assert.Equal(@"\\.\DISPLAY2", monitor.Id),
            monitor => Assert.Equal(@"\\.\DISPLAY10", monitor.Id),
            monitor => Assert.Equal("VirtualMonitor", monitor.Id));
    }
}
