using SlideShowWallpaper.Interop;

namespace SlideShowWallpaper.Services;

public static class WindowCoveragePolicy
{
    private const double FullscreenCoverageRatio = 0.95;
    private const double MaximizedCoverageRatio = 0.5;

    public static bool ShouldPauseVideo(ForegroundWindowInfo? foregroundWindow, NativeMethods.RECT monitorRect, int currentProcessId)
    {
        if (foregroundWindow is null
            || foregroundWindow.Hwnd == IntPtr.Zero
            || foregroundWindow.ProcessId == currentProcessId
            || monitorRect.Width <= 0
            || monitorRect.Height <= 0)
        {
            return false;
        }

        double coverageRatio = CalculateCoverageRatio(foregroundWindow.Rect, monitorRect);
        return foregroundWindow.IsMaximized
            ? coverageRatio >= MaximizedCoverageRatio
            : coverageRatio >= FullscreenCoverageRatio;
    }

    private static double CalculateCoverageRatio(NativeMethods.RECT windowRect, NativeMethods.RECT monitorRect)
    {
        int left = Math.Max(windowRect.Left, monitorRect.Left);
        int top = Math.Max(windowRect.Top, monitorRect.Top);
        int right = Math.Min(windowRect.Right, monitorRect.Right);
        int bottom = Math.Min(windowRect.Bottom, monitorRect.Bottom);
        if (right <= left || bottom <= top)
        {
            return 0;
        }

        double intersectionArea = (double)(right - left) * (bottom - top);
        double monitorArea = (double)monitorRect.Width * monitorRect.Height;
        return monitorArea <= 0 ? 0 : intersectionArea / monitorArea;
    }
}
