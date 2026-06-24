using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;
using Windows.Foundation;
using WinRT.Interop;

namespace SlideShowWallpaper.Windows;

public sealed partial class HardwareOverlayWindow : Window
{
    private readonly IntPtr _hwnd;
    private NativeMethods.RECT _monitorRect;
    private NativeMethods.POINT _dragStartCursor;
    private double _currentX;
    private double _currentY;
    private double _dragStartX;
    private double _dragStartY;
    private bool _isDragging;
    private bool _isClosed;
    private bool _isVisible;

    public HardwareOverlayWindow()
    {
        InitializeComponent();
        Title = LocalizedStrings.Get("HardwareMonitorSettingsGroup");
        _hwnd = WindowNative.GetWindowHandle(this);
        ConfigureWindow();
        ConfigureDrag();
        Closed += (_, _) => _isClosed = true;
    }

    public event EventHandler<HardwareOverlayMovedEventArgs>? HardwareOverlayMoved;

    public void SetHardwareOverlay(HardwareOverlayState state, NativeMethods.RECT monitorRect)
    {
        if (_isClosed)
        {
            return;
        }

        if (!state.IsVisible)
        {
            HideOverlay();
            return;
        }

        _monitorRect = monitorRect;
        HardwareOverlay.Background = HardwareOverlayVisualFactory.CreateBrush(state.BackgroundColor, Microsoft.UI.ColorHelper.FromArgb(153, 0, 0, 0));
        string fontFamily = string.IsNullOrWhiteSpace(state.FontFamily) ? "Segoe UI" : state.FontFamily;
        double fontSize = Math.Max(10, state.FontSize);
        bool hasBackgroundSize = ImageDimensionReader.TryRead(state.BackgroundImagePath, out int backgroundWidth, out int backgroundHeight);
        if (state.Elements.Count > 0 || hasBackgroundSize)
        {
            RenderHardwareOverlayCanvas(state, fontFamily, fontSize, backgroundWidth, backgroundHeight);
        }
        else
        {
            RenderHardwareOverlayRows(state, fontFamily, fontSize);
        }

        HardwareOverlay.Opacity = 1;
        SetOverlayWindowOpacity(state.Opacity);
        ResizeToOverlay();
        SetOverlayPosition(state.X, state.Y);
        if (!_isVisible)
        {
            NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_SHOWNA);
            _isVisible = true;
        }
    }

    public void HideOverlay()
    {
        if (_isClosed)
        {
            return;
        }

        _isDragging = false;
        _isVisible = false;
        NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);
    }

    private void ConfigureWindow()
    {
        Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        NativeMethods.RemoveWindowFrame(_hwnd);
        NativeMethods.SetToolWindow(_hwnd);
        NativeMethods.SetLayeredTransparentWindow(_hwnd, byte.MaxValue);
    }

    private void ConfigureDrag()
    {
        Root.PointerPressed += Root_PointerPressed;
        Root.PointerMoved += Root_PointerMoved;
        Root.PointerReleased += Root_PointerReleased;
        Root.PointerCanceled += Root_PointerCanceled;
        Root.PointerCaptureLost += Root_PointerCaptureLost;
    }

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!NativeMethods.GetCursorPos(out _dragStartCursor))
        {
            return;
        }

        _isDragging = true;
        _dragStartX = _currentX;
        _dragStartY = _currentY;
        Root.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Root_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || !NativeMethods.GetCursorPos(out NativeMethods.POINT cursor))
        {
            return;
        }

        double scale = GetWindowScale();
        SetOverlayPosition(_dragStartX + ((cursor.X - _dragStartCursor.X) / scale), _dragStartY + ((cursor.Y - _dragStartCursor.Y) / scale));
        e.Handled = true;
    }

    private void Root_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        CompleteDrag(notify: true, e);
    }

    private void Root_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        CompleteDrag(notify: false, e);
    }

    private void Root_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        CompleteDrag(notify: true, e);
    }

    private void CompleteDrag(bool notify, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        try
        {
            Root.ReleasePointerCapture(e.Pointer);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }

        if (notify)
        {
            HardwareOverlayMoved?.Invoke(this, new HardwareOverlayMovedEventArgs(_currentX, _currentY));
        }

        e.Handled = true;
    }

    private void RenderHardwareOverlayRows(HardwareOverlayState state, string fontFamily, double fontSize)
    {
        HardwareOverlayLayout layout = HardwareOverlayLayoutCalculator.Calculate([], requestedWidth: state.Width, requestedHeight: state.Height);
        HardwareOverlay.Padding = new Thickness(10, 8, 10, 8);
        HardwareOverlay.Width = layout.Width;
        HardwareOverlay.Height = layout.Height;
        HardwareOverlayRoot.Width = layout.Width;
        HardwareOverlayRoot.Height = layout.Height;
        HardwareOverlayCanvas.Children.Clear();
        HardwareOverlayCanvas.Visibility = Visibility.Collapsed;
        HardwareOverlayBackground.Source = null;
        HardwareOverlayBackground.Visibility = Visibility.Collapsed;
        HardwareOverlayContent.Visibility = Visibility.Visible;
        HardwareOverlayContent.Children.Clear();
        if (!string.IsNullOrWhiteSpace(state.Text))
        {
            HardwareOverlayContent.Children.Add(HardwareOverlayVisualFactory.CreateText(state.Text, fontFamily, fontSize));
        }

        foreach (HardwareOverlayMetric metric in state.Metrics)
        {
            HardwareOverlayContent.Children.Add(HardwareOverlayVisualFactory.CreateMetricRow(metric, fontFamily, fontSize));
        }
    }

    private void RenderHardwareOverlayCanvas(
        HardwareOverlayState state,
        string fontFamily,
        double fontSize,
        int backgroundWidth,
        int backgroundHeight)
    {
        HardwareOverlay.Padding = new Thickness(0);
        HardwareOverlayContent.Children.Clear();
        HardwareOverlayContent.Visibility = Visibility.Collapsed;
        HardwareOverlayCanvas.Children.Clear();

        HardwareOverlayLayout layout = HardwareOverlayLayoutCalculator.Calculate(
            state.Elements,
            backgroundWidth,
            backgroundHeight,
            state.Width,
            state.Height);
        double width = layout.Width;
        double height = layout.Height;
        HardwareOverlay.Width = width;
        HardwareOverlay.Height = height;
        HardwareOverlayRoot.Width = width;
        HardwareOverlayRoot.Height = height;
        HardwareOverlayCanvas.Width = width;
        HardwareOverlayCanvas.Height = height;
        HardwareOverlayCanvas.Visibility = Visibility.Visible;

        if (HardwareOverlayVisualFactory.TryCreateBitmapImage(state.BackgroundImagePath, out var background))
        {
            HardwareOverlayBackground.Source = background;
            HardwareOverlayBackground.Width = width;
            HardwareOverlayBackground.Height = height;
            HardwareOverlayBackground.Visibility = Visibility.Visible;
        }
        else
        {
            HardwareOverlayBackground.Source = null;
            HardwareOverlayBackground.Visibility = Visibility.Collapsed;
        }

        if (state.Elements.Count == 0 && (!string.IsNullOrWhiteSpace(state.Text) || state.Metrics.Count > 0))
        {
            var legacyContent = new StackPanel
            {
                Spacing = 5,
                Padding = new Thickness(10, 8, 10, 8),
            };
            if (!string.IsNullOrWhiteSpace(state.Text))
            {
                legacyContent.Children.Add(HardwareOverlayVisualFactory.CreateText(state.Text, fontFamily, fontSize));
            }

            foreach (HardwareOverlayMetric metric in state.Metrics)
            {
                legacyContent.Children.Add(HardwareOverlayVisualFactory.CreateMetricRow(metric, fontFamily, fontSize));
            }

            Canvas.SetLeft(legacyContent, 0);
            Canvas.SetTop(legacyContent, 0);
            HardwareOverlayCanvas.Children.Add(legacyContent);
        }

        foreach (HardwareOverlayElementState element in state.Elements)
        {
            UIElement visual = HardwareOverlayVisualFactory.CreateElement(element);
            Canvas.SetLeft(visual, element.X);
            Canvas.SetTop(visual, element.Y);
            HardwareOverlayCanvas.Children.Add(visual);
        }
    }

    private void ResizeToOverlay()
    {
        (double width, double height) = MeasureOverlay();
        double scale = GetWindowScale();
        NativeMethods.SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            0,
            0,
            Math.Max(1, (int)Math.Ceiling(width * scale)),
            Math.Max(1, (int)Math.Ceiling(height * scale)),
            (uint)(NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE));
    }

    private (double Width, double Height) MeasureOverlay()
    {
        HardwareOverlay.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double width = HardwareOverlay.DesiredSize.Width;
        double height = HardwareOverlay.DesiredSize.Height;
        if (width <= 0)
        {
            width = double.IsNaN(HardwareOverlay.Width) ? 1 : HardwareOverlay.Width;
        }

        if (height <= 0)
        {
            height = double.IsNaN(HardwareOverlay.Height) ? 1 : HardwareOverlay.Height;
        }

        return (Math.Max(1, width), Math.Max(1, height));
    }

    private void SetOverlayPosition(double x, double y)
    {
        (double width, double height) = MeasureOverlay();
        double scale = GetWindowScale();
        double monitorWidth = Math.Max(1, _monitorRect.Width / scale);
        double monitorHeight = Math.Max(1, _monitorRect.Height / scale);
        _currentX = Math.Clamp(x, 0, Math.Max(0, monitorWidth - width));
        _currentY = Math.Clamp(y, 0, Math.Max(0, monitorHeight - height));
        int screenX = _monitorRect.Left + (int)Math.Round(_currentX * scale);
        int screenY = _monitorRect.Top + (int)Math.Round(_currentY * scale);
        NativeMethods.SetWindowPos(
            _hwnd,
            IntPtr.Zero,
            screenX,
            screenY,
            0,
            0,
            (uint)(NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE));
    }

    private void SetOverlayWindowOpacity(double opacity)
    {
        byte alpha = (byte)Math.Round(Math.Clamp(opacity, 0.1, 1) * byte.MaxValue);
        NativeMethods.SetLayeredTransparentWindow(_hwnd, alpha);
    }

    private double GetWindowScale()
    {
        double xamlScale = Root.XamlRoot?.RasterizationScale ?? 0;
        if (xamlScale > 0)
        {
            return xamlScale;
        }

        uint dpi = NativeMethods.GetDpiForWindow(_hwnd);
        return dpi > 0 ? dpi / 96.0 : 1;
    }

}
