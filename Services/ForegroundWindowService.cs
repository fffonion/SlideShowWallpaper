using System.Runtime.InteropServices;
using SlideShowWallpaper.Interop;

namespace SlideShowWallpaper.Services;

public sealed class ForegroundWindowService
{
    public ForegroundWindowInfo? GetForegroundWindowInfo()
    {
        IntPtr hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT rect))
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
}

public sealed record ForegroundWindowInfo(IntPtr Hwnd, int ProcessId, bool IsMaximized, NativeMethods.RECT Rect);
