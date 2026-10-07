using System.Runtime.InteropServices;

namespace SlideShowWallpaper.Interop;

public static partial class NativeMethods
{
    internal const int WM_WTSSESSION_CHANGE = 0x02B1;

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSRegisterSessionNotification(IntPtr windowHandle, uint flags);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WTSUnRegisterSessionNotification(IntPtr windowHandle);
}
