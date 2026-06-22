using SlideShowWallpaper.Interop;

namespace SlideShowWallpaper.Tests;

public sealed class WindowStyleTests
{
    [Fact]
    public void ToBorderlessWindowStyle_WithOverlappedFrame_RemovesNonClientFrame()
    {
        nint style = NativeMethods.WS_VISIBLE
            | NativeMethods.WS_CAPTION
            | NativeMethods.WS_THICKFRAME
            | NativeMethods.WS_SYSMENU
            | NativeMethods.WS_MINIMIZEBOX
            | NativeMethods.WS_MAXIMIZEBOX;

        nint borderless = NativeMethods.ToBorderlessWindowStyle(style);

        Assert.Equal(NativeMethods.WS_VISIBLE | NativeMethods.WS_POPUP, borderless);
    }
}
