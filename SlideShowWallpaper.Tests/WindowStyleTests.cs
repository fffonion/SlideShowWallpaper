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

    [Fact]
    public void ToBorderlessWindowStyle_WhenShowWindowIsFalse_KeepsWindowHidden()
    {
        nint style = NativeMethods.WS_VISIBLE
            | NativeMethods.WS_CAPTION;

        nint borderless = NativeMethods.ToBorderlessWindowStyle(style, showWindow: false);

        Assert.Equal(NativeMethods.WS_POPUP, borderless);
    }

    [Fact]
    public void ToDesktopChildWindowStyle_WithPopupMinimizedWindow_ConvertsToVisibleChild()
    {
        nint style = NativeMethods.WS_VISIBLE
            | NativeMethods.WS_POPUP
            | NativeMethods.WS_CAPTION
            | NativeMethods.WS_THICKFRAME
            | NativeMethods.WS_MINIMIZE;

        nint child = NativeMethods.ToDesktopChildWindowStyle(style, showWindow: true);

        Assert.True((child & NativeMethods.WS_CHILD) != 0);
        Assert.True((child & NativeMethods.WS_CLIPCHILDREN) != 0);
        Assert.True((child & NativeMethods.WS_CLIPSIBLINGS) != 0);
        Assert.True((child & NativeMethods.WS_VISIBLE) != 0);
        Assert.Equal(0, child & NativeMethods.WS_POPUP);
        Assert.Equal(0, child & NativeMethods.WS_MINIMIZE);
        Assert.Equal(0, child & NativeMethods.WS_CAPTION);
        Assert.Equal(0, child & NativeMethods.WS_THICKFRAME);
    }

    [Fact]
    public void ToDesktopChildWindowStyle_WhenShowWindowIsFalse_KeepsChildHidden()
    {
        nint style = NativeMethods.WS_VISIBLE | NativeMethods.WS_POPUP;

        nint child = NativeMethods.ToDesktopChildWindowStyle(style, showWindow: false);

        Assert.True((child & NativeMethods.WS_CHILD) != 0);
        Assert.Equal(0, child & NativeMethods.WS_VISIBLE);
        Assert.Equal(0, child & NativeMethods.WS_POPUP);
    }
}
