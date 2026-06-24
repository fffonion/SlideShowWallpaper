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

    [Fact]
    public void CreateShellViewInsertAfterWindow_WithIconList_UsesIconList()
    {
        var iconList = new IntPtr(42);

        IntPtr insertAfter = DesktopHostService.CreateShellViewInsertAfterWindow(iconList);

        Assert.Equal(iconList, insertAfter);
    }

    [Fact]
    public void CreateShellViewInsertAfterWindow_WithoutIconList_UsesBottom()
    {
        IntPtr insertAfter = DesktopHostService.CreateShellViewInsertAfterWindow(IntPtr.Zero);

        Assert.NotEqual(IntPtr.Zero, insertAfter);
    }

    [Fact]
    public void HostOverlayOnDesktop_ReturnsHostOffsetAndAvoidsBottomZOrder()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "DesktopHostService.cs"));
        string method = source[source.IndexOf("public DesktopHostOffset HostOverlayOnDesktop", StringComparison.Ordinal)..];

        Assert.Contains("NativeMethods.SetParent(hwnd, target.HostWindow)", method);
        Assert.Contains("return new DesktopHostOffset(hostRect.Left, hostRect.Top);", method);
        Assert.Contains("insertAfterWindow == NativeMethods.HWND_BOTTOM", method);
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
