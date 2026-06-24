using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SlideShowWallpaper.Services;

public static class WorkingSetTrimmer
{
    public static bool TrimCurrentProcess()
    {
        using Process process = Process.GetCurrentProcess();
        return Trim(process);
    }

    public static bool Trim(Process process)
    {
        return EmptyWorkingSet(process.Handle);
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr processHandle);
}
