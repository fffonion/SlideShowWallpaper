namespace SlideShowWallpaper.Tests;

public sealed class NativeMethodsSourceTests
{
    [Fact]
    public void LayeredTransparentWindow_UsesColorKeyAndAlpha()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Interop", "NativeMethods.cs"));

        Assert.Contains("WS_EX_LAYERED", source);
        Assert.Contains("LWA_COLORKEY | LWA_ALPHA", source);
        Assert.Contains("SetLayeredWindowAttributes(hWnd, ComposeColorRef(1, 2, 3), alpha", source);
        Assert.Contains("return (uint)(red | (green << 8) | (blue << 16));", source);
    }

    [Fact]
    public void CurrentProcessHandle_UsesManagedProcessHandleInsteadOfPInvoke()
    {
        string root = FindProjectRoot();
        string nativeSource = File.ReadAllText(Path.Combine(root, "Interop", "NativeMethods.cs"));
        string restartSource = File.ReadAllText(Path.Combine(root, "Services", "UnelevatedRestartService.cs"));
        string trimmerSource = File.ReadAllText(Path.Combine(root, "Services", "ProcessMemoryTrimmer.cs"));

        Assert.DoesNotContain("GetCurrentProcess()", nativeSource);
        Assert.Contains("Process.GetCurrentProcess()", restartSource);
        Assert.Contains("Process.GetCurrentProcess()", trimmerSource);
        Assert.DoesNotContain("NativeMethods.GetCurrentProcess()", restartSource);
        Assert.DoesNotContain("NativeMethods.GetCurrentProcess()", trimmerSource);
    }

    [Fact]
    public void WindowCoverageEnumeration_UsesVisibilityAndMinimizedFilters()
    {
        string root = FindProjectRoot();
        string nativeSource = File.ReadAllText(Path.Combine(root, "Interop", "NativeMethods.cs"));
        string foregroundSource = File.ReadAllText(Path.Combine(root, "Services", "ForegroundWindowService.cs"));

        Assert.Contains("EnumWindows", nativeSource);
        Assert.Contains("IsWindowVisible", nativeSource);
        Assert.Contains("IsIconic", nativeSource);
        Assert.Contains("GetClassName", nativeSource);
        Assert.Contains("GetVisibleWindowInfos", foregroundSource);
        Assert.Contains("if (!IsVisibleTopLevelWindow(hwnd))", foregroundSource);
        Assert.Contains("NativeMethods.EnumWindows", foregroundSource);
        Assert.Contains("NativeMethods.IsWindowVisible(hwnd)", foregroundSource);
        Assert.Contains("!NativeMethods.IsIconic(hwnd)", foregroundSource);
        Assert.Contains("!IsShellWindowClass(GetWindowClassName(hwnd))", foregroundSource);
        Assert.Contains("className is \"Progman\" or \"WorkerW\" or \"Shell_TrayWnd\" or \"Shell_SecondaryTrayWnd\"", foregroundSource);
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
