using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Services;
using Windows.Graphics;

namespace SlideShowWallpaper;

public sealed partial class MainWindow
{
    private const int PreferredSettingsWindowWidth = 1178;

    private void ConfigureSettingsWindow()
    {
        RectInt32 workArea = GetPreferredSettingsWorkArea();
        int width = GetPreferredSettingsWindowWidth(workArea);
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
        int logicalWidth = (int)Math.Ceiling(targetWidth / scale);
        int measuredHeight = _isHardwareEditorSelected
            ? EstimateWindowHeightForHardwareEditorPage()
            : _isSettingsSelected
                ? EstimateWindowHeightForSettingsPage()
                : EstimateWindowHeightForMonitorPage(logicalWidth);

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
        int width = AppWindow.Size.Width > 0 ? AppWindow.Size.Width : GetPreferredSettingsWindowWidth(workArea);
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
            double logicalHeight = MeasureSettingsContentHeight(targetWidth / scale);
            if (!double.IsFinite(logicalHeight) || logicalHeight <= 0)
            {
                return estimatedHeight;
            }

            int measuredHeight = (int)Math.Ceiling(logicalHeight * scale);
            return Math.Clamp(measuredHeight, Math.Min(minimumHeight, maximumHeight), maximumHeight);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return estimatedHeight;
        }
    }

    private double MeasureSettingsContentHeight(double logicalWindowWidth)
    {
        if (MonitorContent.Content is not FrameworkElement content)
        {
            return 0;
        }

        double titleBarHeight = AppTitleBar.ActualHeight > 0 ? AppTitleBar.ActualHeight : 48;
        double contentFrameHeight = ContentFrame.Padding.Top
            + ContentFrame.Padding.Bottom
            + ContentFrame.BorderThickness.Top
            + ContentFrame.BorderThickness.Bottom;
        double contentWidth = MonitorContent.ActualWidth > 0
            ? MonitorContent.ActualWidth
            : EstimateSettingsContentWidth(logicalWindowWidth);
        double contentHeight = MeasureIntrinsicContentHeight(content, contentWidth);
        return titleBarHeight + contentFrameHeight + contentHeight;
    }

    private static double EstimateSettingsContentWidth(double logicalWindowWidth)
    {
        const double monitorLayoutLeftPadding = 16;
        const double monitorLayoutNegativeMargin = -8;
        const double navigationWidth = 228;
        const double monitorLayoutColumnSpacing = 8;
        const double contentBorderThickness = 2;
        const double contentHorizontalPadding = 32;
        return Math.Max(
            0,
            logicalWindowWidth
            - monitorLayoutLeftPadding
            - monitorLayoutNegativeMargin
            - navigationWidth
            - monitorLayoutColumnSpacing
            - contentBorderThickness
            - contentHorizontalPadding);
    }

    private static double MeasureIntrinsicContentHeight(FrameworkElement element, double logicalWidth)
    {
        FrameworkElement measureTarget = element is ScrollViewer scrollViewer && scrollViewer.Content is FrameworkElement scrollContent
            ? scrollContent
            : element;
        measureTarget.Measure(new global::Windows.Foundation.Size(logicalWidth, double.PositiveInfinity));
        return measureTarget.DesiredSize.Height;
    }

    private void MoveAndResizeSettingsWindow(RectInt32 workArea, int width, int height)
    {
        int x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
        int y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);
        AppWindow.Resize(new SizeInt32(width, height));
        AppWindow.Move(new PointInt32(x, y));
        NativeMethods.SetWindowPos(_hwnd, IntPtr.Zero, x, y, width, height, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private int GetPreferredSettingsWindowWidth(RectInt32 workArea)
    {
        return Math.Min((int)Math.Ceiling(PreferredSettingsWindowWidth * GetWindowScale()), workArea.Width);
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
            return new RectInt32(0, 0, 1540, 980);
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
        const int sectionSpacing = 14;
        return titleBarHeight
            + contentTopPadding
            + EstimateSettingsSectionHeight(true, 9)
            + sectionSpacing
            + EstimateSettingsSectionHeight(true, 3)
            + contentBottomPadding;
    }

    private static int EstimateWindowHeightForHardwareEditorPage()
    {
        const int titleBarHeight = 48;
        const int contentTopPadding = 12;
        const int contentBottomPadding = 24;
        const int editorCanvasSectionHeight = 540;
        return titleBarHeight
            + contentTopPadding
            + editorCanvasSectionHeight
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
            CancelBackgroundMemoryTrim();
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
            if (_settingsUiUnloadedForBackground)
            {
                AppLog.Write("Background startup memory trim kept after hidden restore message.");
                return;
            }

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
        _coordinator.HardwareOverlayMoved -= Coordinator_HardwareOverlayMoved;
        _hardwareMonitorService.BrokerProcessStarted -= HardwareMonitorService_BrokerProcessStarted;
        _trayIconService.Dispose();
        _coordinator.Shutdown();
        _hardwareMonitorService.Dispose();
    }
}
