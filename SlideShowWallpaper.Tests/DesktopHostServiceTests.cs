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
    public void DesktopHostService_DoesNotExposeOverlayHosting()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "DesktopHostService.cs"));

        Assert.DoesNotContain("HostOverlayOnDesktop", source);
        Assert.DoesNotContain("DesktopHostOffset", source);
    }

    [Fact]
    public void HostOnDesktop_ConvertsWallpaperWindowToChildBeforeSetParent()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "DesktopHostService.cs"));
        string method = source[
            source.IndexOf("public void HostOnDesktop", StringComparison.Ordinal)..
            source.IndexOf("public static uint CreatePositionFlags", StringComparison.Ordinal)];

        int childStyleIndex = method.IndexOf("NativeMethods.SetDesktopChildWindow(hwnd);", StringComparison.Ordinal);
        int setParentIndex = method.IndexOf("NativeMethods.SetParent(hwnd, target.HostWindow);", StringComparison.Ordinal);
        int showIndex = method.IndexOf("NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOWNA);", StringComparison.Ordinal);

        Assert.True(childStyleIndex >= 0);
        Assert.True(setParentIndex > childStyleIndex);
        Assert.True(showIndex > setParentIndex);
        Assert.DoesNotContain("SW_SHOW);", method);
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
