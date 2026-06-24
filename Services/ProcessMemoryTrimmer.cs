using System.Diagnostics;
using System.Runtime;

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
            WorkingSetTrimmer.Trim(process);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }
}
