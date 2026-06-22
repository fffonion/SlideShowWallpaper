using Microsoft.UI.Xaml;
using SlideShowWallpaper.Interop;
using WinRT.Interop;

namespace SlideShowWallpaper.Services;

public sealed class DesktopHostService
{
    public void HostOnDesktop(Window window, string monitorId, IReadOnlyDictionary<string, NativeMethods.RECT> monitorRects)
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(window);

        DesktopHostTarget target = GetDesktopHostTarget();
        NativeMethods.RECT hostRect = default;
        if (target.HostWindow != IntPtr.Zero)
        {
            NativeMethods.SetParent(hwnd, target.HostWindow);
            NativeMethods.GetWindowRect(target.HostWindow, out hostRect);
            if (target.IconListWindow != IntPtr.Zero)
            {
                MakeDesktopIconListTransparent(target.IconListWindow);
            }
        }

        if (monitorRects.TryGetValue(monitorId, out NativeMethods.RECT rect))
        {
            int x = target.HostWindow == IntPtr.Zero ? rect.Left : rect.Left - hostRect.Left;
            int y = target.HostWindow == IntPtr.Zero ? rect.Top : rect.Top - hostRect.Top;
            NativeMethods.SetWindowPos(hwnd, target.InsertAfterWindow, x, y, rect.Width, rect.Height, CreatePositionFlags(target.InsertAfterWindow));
        }
    }

    public static uint CreatePositionFlags(IntPtr insertAfterWindow)
    {
        return insertAfterWindow == IntPtr.Zero
            ? (uint)(NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE)
            : (uint)NativeMethods.SWP_NOACTIVATE;
    }

    public static IntPtr CreateShellViewInsertAfterWindow(IntPtr iconListWindow)
    {
        return iconListWindow == IntPtr.Zero ? NativeMethods.HWND_BOTTOM : iconListWindow;
    }

    private static DesktopHostTarget GetDesktopHostTarget()
    {
        IntPtr progman = NativeMethods.FindWindow("Progman", null);
        if (progman != IntPtr.Zero)
        {
            NativeMethods.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
        }

        var target = new DesktopHostTarget(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        NativeMethods.EnumWindows((topHandle, _) =>
        {
            IntPtr shellView = NativeMethods.FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                IntPtr iconList = NativeMethods.FindWindowEx(shellView, IntPtr.Zero, "SysListView32", null);
                IntPtr workerW = NativeMethods.FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                target = workerW != IntPtr.Zero
                    ? new DesktopHostTarget(workerW, IntPtr.Zero, IntPtr.Zero)
                    : new DesktopHostTarget(shellView, CreateShellViewInsertAfterWindow(iconList), iconList);
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return target.HostWindow != IntPtr.Zero ? target : new DesktopHostTarget(progman, NativeMethods.HWND_BOTTOM, IntPtr.Zero);
    }

    private static void MakeDesktopIconListTransparent(IntPtr listView)
    {
        NativeMethods.SendMessage(listView, NativeMethods.LVM_SETBKCOLOR, IntPtr.Zero, NativeMethods.CLR_NONE);
        NativeMethods.SendMessage(listView, NativeMethods.LVM_SETTEXTBKCOLOR, IntPtr.Zero, NativeMethods.CLR_NONE);
        NativeMethods.InvalidateRect(listView, IntPtr.Zero, true);
    }

    private readonly record struct DesktopHostTarget(IntPtr HostWindow, IntPtr InsertAfterWindow, IntPtr IconListWindow);
}
