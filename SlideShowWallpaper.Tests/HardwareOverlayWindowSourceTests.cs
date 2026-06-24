namespace SlideShowWallpaper.Tests;

public sealed class HardwareOverlayWindowSourceTests
{
    [Fact]
    public void HardwareOverlayWindow_ConfiguresBorderlessDraggableOverlay()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "HardwareOverlayWindow.xaml.cs"));

        Assert.Contains("presenter.SetBorderAndTitleBar(false, false);", source);
        Assert.Contains("Root.PointerPressed += Root_PointerPressed;", source);
        Assert.Contains("Root.PointerMoved += Root_PointerMoved;", source);
        Assert.Contains("Root.PointerReleased += Root_PointerReleased;", source);
        Assert.Contains("NativeMethods.GetCursorPos(out _dragStartCursor)", source);
        Assert.Contains("SetOverlayPosition(_dragStartX + ((cursor.X - _dragStartCursor.X) / scale)", source);
        Assert.Contains("HardwareOverlayMoved?.Invoke(this, new HardwareOverlayMovedEventArgs(_currentX, _currentY));", source);
    }

    [Fact]
    public void HardwareOverlayWindow_PositionsRelativeToTargetMonitor()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "HardwareOverlayWindow.xaml.cs"));

        Assert.Contains("public void SetHardwareOverlay(HardwareOverlayState state, NativeMethods.RECT monitorRect)", source);
        Assert.Contains("_monitorRect = monitorRect;", source);
        Assert.Contains("Root.XamlRoot?.RasterizationScale", source);
        Assert.Contains("new PointInt32(_monitorRect.Left +", source);
        Assert.Contains("NativeMethods.SW_SHOWNA", source);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SlideShowWallpaper.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Project root not found.");
    }
}
