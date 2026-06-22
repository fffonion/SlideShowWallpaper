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
            NativeMethods.EmptyWorkingSet(NativeMethods.GetCurrentProcess());
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }
}
