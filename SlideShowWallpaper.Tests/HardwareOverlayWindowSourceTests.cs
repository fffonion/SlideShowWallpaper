namespace SlideShowWallpaper.Tests;

public sealed class HardwareOverlayWindowSourceTests
{
    [Fact]
    public void HardwareOverlayWindow_ConfiguresBorderlessDraggableOverlay()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "HardwareOverlayWindow.xaml.cs"));

        Assert.Contains("Title = LocalizedStrings.Get(\"HardwareMonitorSettingsGroup\");", source);
        Assert.Contains("presenter.SetBorderAndTitleBar(false, false);", source);
        Assert.Contains("Root.PointerPressed += Root_PointerPressed;", source);
        Assert.Contains("Root.PointerMoved += Root_PointerMoved;", source);
        Assert.Contains("Root.PointerReleased += Root_PointerReleased;", source);
        Assert.Contains("NativeMethods.GetCursorPos(out _dragStartCursor)", source);
        Assert.Contains("SetOverlayPosition(_dragStartX + ((cursor.X - _dragStartCursor.X) / scale)", source);
        Assert.Contains("HardwareOverlayMoved?.Invoke(this, new HardwareOverlayMovedEventArgs(_currentX, _currentY));", source);
        Assert.DoesNotContain("SetDesktopHostOrigin", source);
    }

    [Fact]
    public void HardwareOverlayWindow_PositionsRelativeToTargetMonitor()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "HardwareOverlayWindow.xaml.cs"));

        Assert.Contains("public void SetHardwareOverlay(HardwareOverlayState state, NativeMethods.RECT monitorRect)", source);
        Assert.Contains("_monitorRect = monitorRect;", source);
        Assert.Contains("Root.XamlRoot?.RasterizationScale", source);
        Assert.Contains("NativeMethods.SetWindowPos(", source);
        Assert.Contains("screenX,", source);
        Assert.Contains("screenY,", source);
        Assert.DoesNotContain("_desktopHostOrigin", source);
        Assert.Contains("NativeMethods.SW_SHOWNA", source);
    }

    [Fact]
    public void HardwareOverlayWindow_DoesNotShowAgainOnEveryRefresh()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "HardwareOverlayWindow.xaml.cs"));

        Assert.Contains("private bool _isVisible;", source);
        Assert.Contains("if (!_isVisible)", source);
        Assert.Contains("_isVisible = true;", source);
        Assert.Contains("_isVisible = false;", source);
    }

    [Fact]
    public void HardwareOverlayWindow_UsesSharedVisualFactory()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "HardwareOverlayWindow.xaml.cs"));

        Assert.Contains("HardwareOverlayVisualFactory.CreateText", source);
        Assert.Contains("HardwareOverlayVisualFactory.CreateMetricRow", source);
        Assert.Contains("HardwareOverlayVisualFactory.CreateElement(element)", source);
        Assert.DoesNotContain("private static UIElement CreateHardwareOverlayElement", source);
        Assert.DoesNotContain("private static UIElement CreateHardwareOverlaySensorElement", source);
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
