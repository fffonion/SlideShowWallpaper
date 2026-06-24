using System.Diagnostics;
using System.Runtime;
using SlideShowWallpaper.Interop;

namespace SlideShowWallpaper.Services;

public static class ProcessMemoryTrimmer
{
    public static void TrimCurrentProcess()
    {
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            using Process process = Process.GetCurrentProcess();
            NativeMethods.EmptyWorkingSet(process.Handle);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }
}
