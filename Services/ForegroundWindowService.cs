using System.Runtime.InteropServices;
using System.Text;
using SlideShowWallpaper.Interop;

namespace SlideShowWallpaper.Services;

public sealed class ForegroundWindowService
{
    private const int ClassNameCapacity = 256;

    public ForegroundWindowInfo? GetForegroundWindowInfo()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (!IsVisibleTopLevelWindow(hwnd))
        {
            return null;
        }

        return GetWindowInfo(hwnd);
    }

    public IReadOnlyList<ForegroundWindowInfo> GetVisibleWindowInfos()
    {
        var windows = new List<ForegroundWindowInfo>();
        _ = NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (IsVisibleTopLevelWindow(hwnd) && GetWindowInfo(hwnd) is { } info)
            {
                windows.Add(info);
            }

            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public ForegroundWindowInfo? GetWindowInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect))
        {
            return null;
        }

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
        var placement = new NativeMethods.WINDOWPLACEMENT
        {
            length = Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>(),
        };
        bool isMaximized = NativeMethods.GetWindowPlacement(hwnd, ref placement)
            && placement.showCmd == NativeMethods.SW_SHOWMAXIMIZED;

        return new ForegroundWindowInfo(hwnd, (int)processId, isMaximized, rect);
    }

    private static bool IsVisibleTopLevelWindow(IntPtr hwnd)
    {
        return hwnd != IntPtr.Zero
            && NativeMethods.IsWindowVisible(hwnd)
            && !NativeMethods.IsIconic(hwnd)
            && !IsShellWindowClass(GetWindowClassName(hwnd));
    }

    private static string GetWindowClassName(IntPtr hwnd)
    {
        var builder = new StringBuilder(ClassNameCapacity);
        int length = NativeMethods.GetClassName(hwnd, builder, builder.Capacity);
        return length <= 0 ? string.Empty : builder.ToString();
    }

    private static bool IsShellWindowClass(string className)
    {
        return className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }
}

public sealed record ForegroundWindowInfo(IntPtr Hwnd, int ProcessId, bool IsMaximized, NativeMethods.RECT Rect);
