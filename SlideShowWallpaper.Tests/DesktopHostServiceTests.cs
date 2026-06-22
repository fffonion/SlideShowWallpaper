using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class DesktopHostServiceTests
{
    [Fact]
    public void CreatePositionFlags_WithInsertAfterWindow_AllowsZOrderChange()
    {
        uint flags = DesktopHostService.CreatePositionFlags(new IntPtr(42));

        Assert.Equal(0x0010u, flags);
    }

    [Fact]
    public void CreatePositionFlags_WithoutInsertAfterWindow_PreservesZOrder()
    {
        uint flags = DesktopHostService.CreatePositionFlags(IntPtr.Zero);

        Assert.Equal(0x0014u, flags);
    }
}
