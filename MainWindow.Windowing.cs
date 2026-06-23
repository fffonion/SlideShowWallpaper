using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Services;
using Windows.Graphics;

namespace SlideShowWallpaper;

public sealed partial class MainWindow
{
    private void ConfigureSettingsWindow()
    {
        const int preferredWidth = 1460;
        RectInt32 workArea = GetPreferredSettingsWorkArea();
        int width = Math.Min(preferredWidth, workArea.Width);
        int height = CalculatePreferredWindowHeight(width, workArea.Height);
        MoveAndResizeSettingsWindow(workArea, width, height);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
        }
    }

    private int CalculatePreferredWindowHeight(int targetWidth, int maximumHeight)
    {
        const int minimumHeight = 720;
        double scale = GetWindowScale();
        int measuredHeight = _isSettingsSelected
            ? EstimateWindowHeightForSettingsPage()
            : EstimateWindowHeightForMonitorPage(targetWidth);

        int physicalHeight = (int)Math.Ceiling(measuredHeight * scale);
        return Math.Clamp(physicalHeight, Math.Min(minimumHeight, maximumHeight), maximumHeight);
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        if (_contentHeightAdjusted || _settingsUiUnloadedForBackground)
        {
            return;
        }

        _contentHeightAdjusted = true;
        ResizeToMeasuredContentHeight();
    }

    private void ResizeToMeasuredContentHeight()
    {
        RectInt32 workArea = GetPreferredSettingsWorkArea();
        int width = AppWindow.Size.Width > 0 ? AppWindow.Size.Width : Math.Min(1460, workArea.Width);
        int height = CalculateMeasuredWindowHeight(width, workArea.Height);
        MoveAndResizeSettingsWindow(workArea, width, height);
    }

    private int CalculateMeasuredWindowHeight(int targetWidth, int maximumHeight)
    {
        const int minimumHeight = 720;
        double scale = GetWindowScale();
        int estimatedHeight = CalculatePreferredWindowHeight(targetWidth, maximumHeight);
        try
        {
            Root.Measure(new global::Windows.Foundation.Size(targetWidth / scale, maximumHeight / scale));
            int measuredHeight = double.IsFinite(Root.DesiredSize.Height) && Root.DesiredSize.Height > 0
                ? (int)Math.Ceiling(Root.DesiredSize.Height * scale)
                : estimatedHeight;
            return Math.Clamp(Math.Max(measuredHeight, estimatedHeight), Math.Min(minimumHeight, maximumHeight), maximumHeight);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return estimatedHeight;
        }
    }

    private void MoveAndResizeSettingsWindow(RectInt32 workArea, int width, int height)
    {
        int x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
        int y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);
        AppWindow.Resize(new SizeInt32(width, height));
        AppWindow.Move(new PointInt32(x, y));
        NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, x, y, width, height, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private double GetWindowScale()
    {
        uint dpi = _hwnd == IntPtr.Zero ? 96 : NativeMethods.GetDpiForWindow(_hwnd);
        return Math.Max(1, dpi / 96.0);
    }

    private static RectInt32 GetPreferredSettingsWorkArea()
    {
        NativeMethods.RECT best = default;
        long bestArea = -1;
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr _, ref NativeMethods.RECT _, IntPtr _) =>
        {
            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
            };
            if (NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                long area = (long)info.rcWork.Width * info.rcWork.Height;
                if (area > bestArea)
                {
                    best = info.rcWork;
                    bestArea = area;
                }
            }

            return true;
        }, IntPtr.Zero);

        if (bestArea <= 0)
        {
            return new RectInt32(0, 0, 1460, 980);
        }

        return new RectInt32(best.Left, best.Top, best.Width, best.Height);
    }

    private static int EstimateWindowHeightForMonitorPage(int targetWidth)
    {
        const int titleBarHeight = 48;
        const int contentTopPadding = 12;
        const int contentBottomPadding = 24;
        const int monitorHeaderHeight = 48;
        const int monitorRowSpacing = 8;
        const int sectionSpacing = 14;
        const int previewBottomSlack = 20;
        int settingsHeight =
            EstimateSettingsSectionHeight(false, 1)
            + EstimateSettingsSectionHeight(true, 2)
            + EstimateSettingsSectionHeight(true, 5)
            + EstimateSettingsSectionHeight(true, 3)
            + (sectionSpacing * 3);
        int narrowWidthSlack = targetWidth < 1200 ? 48 : 0;

        return titleBarHeight
            + contentTopPadding
            + monitorHeaderHeight
            + monitorRowSpacing
            + settingsHeight
            + contentBottomPadding
            + previewBottomSlack
            + narrowWidthSlack;
    }

    private static int EstimateWindowHeightForSettingsPage()
    {
        const int titleBarHeight = 48;
        const int contentTopPadding = 12;
        const int contentBottomPadding = 24;
        return titleBarHeight
            + contentTopPadding
            + EstimateSettingsSectionHeight(true, 8)
            + contentBottomPadding;
    }

    private static int EstimateSettingsSectionHeight(bool hasTitle, int rowCount)
    {
        const int sectionBorder = 2;
        const int titleBlockHeight = 48;
        const int rowHeight = 50;
        const int dividerHeight = 1;
        int titleHeight = hasTitle ? titleBlockHeight + dividerHeight : 0;
        int dividers = Math.Max(0, rowCount - 1) * dividerHeight;
        return sectionBorder + titleHeight + (rowCount * rowHeight) + dividers;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _settingsApplyTimer.Stop();
        if (_exitRequested || !_viewModel.CloseToTray)
        {
            _currentImageCheckpointTimer.Stop();
            _playbackStatusTimer.Stop();
            UnloadPreviewState();
            ShutdownApplication();
            return;
        }

        args.Handled = true;
        UnloadSettingsUiForTray();
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
    }

    public void ShowSettingsWindow()
    {
        EnsureSettingsUiLoaded();

        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOW);
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_RESTORE);
        Activate();
        NativeMethods.SetForegroundWindow(_hwnd);
    }

    private void HandleWindowMinimizedChanged(bool isMinimized)
    {
        if (_exitRequested)
        {
            return;
        }

        if (isMinimized)
        {
            UnloadSettingsUiForBackground();
        }
        else
        {
            EnsureSettingsUiLoaded();
        }
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        Close();
    }

    private void ShutdownApplication()
    {
        DisposePreviewPopup();
        _coordinator.OrderedImagesChanged -= Coordinator_OrderedImagesChanged;
        _coordinator.CurrentWallpaperChanged -= Coordinator_CurrentWallpaperChanged;
        _trayIconService.Dispose();
        _coordinator.Shutdown();
    }
}
