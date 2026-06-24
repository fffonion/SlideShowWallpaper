using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Windows;

internal static class HardwareOverlayIconFactory
{
    public static Canvas CreateIcon(HardwareOverlayIconKind kind, double size, Brush? brush = null)
    {
        var canvas = new Canvas
        {
            Width = size,
            Height = size,
            VerticalAlignment = VerticalAlignment.Center,
        };
        brush ??= new SolidColorBrush(Microsoft.UI.Colors.White);
        double scale = size / 20;
        switch (kind)
        {
            case HardwareOverlayIconKind.Cpu:
                DrawChip(canvas, brush, scale);
                break;
            case HardwareOverlayIconKind.Gpu:
                DrawGpu(canvas, brush, scale);
                break;
            case HardwareOverlayIconKind.Storage:
                DrawStorage(canvas, brush, scale);
                break;
            case HardwareOverlayIconKind.Temperature:
                DrawThermometer(canvas, brush, scale);
                break;
            case HardwareOverlayIconKind.Fan:
                DrawFan(canvas, brush, scale);
                break;
            case HardwareOverlayIconKind.Memory:
                DrawMemory(canvas, brush, scale);
                break;
            case HardwareOverlayIconKind.Vram:
                DrawGpu(canvas, brush, scale);
                DrawMemoryCells(canvas, brush, scale, 11, 12);
                break;
            case HardwareOverlayIconKind.Power:
                DrawBolt(canvas, brush, scale);
                break;
            default:
                DrawGeneric(canvas, brush, scale);
                break;
        }

        return canvas;
    }

    private static void DrawChip(Canvas canvas, Brush brush, double scale)
    {
        AddRoundedRect(canvas, 5, 5, 10, 10, brush, scale, strokeOnly: true);
        AddRoundedRect(canvas, 8, 8, 4, 4, brush, scale, strokeOnly: false);
        for (int index = 0; index < 3; index++)
        {
            double offset = 6 + (index * 4);
            AddRect(canvas, offset, 2, 1.3, 3, brush, scale);
            AddRect(canvas, offset, 15, 1.3, 3, brush, scale);
            AddRect(canvas, 2, offset, 3, 1.3, brush, scale);
            AddRect(canvas, 15, offset, 3, 1.3, brush, scale);
        }
    }

    private static void DrawGpu(Canvas canvas, Brush brush, double scale)
    {
        AddRoundedRect(canvas, 3, 6, 13, 8, brush, scale, strokeOnly: true);
        AddRect(canvas, 16, 8, 2, 4, brush, scale);
        AddRect(canvas, 6, 14, 6, 2, brush, scale);
        AddEllipse(canvas, 7, 8, 4, 4, brush, scale, strokeOnly: true);
    }

    private static void DrawStorage(Canvas canvas, Brush brush, double scale)
    {
        AddRoundedRect(canvas, 4, 5, 12, 10, brush, scale, strokeOnly: true);
        AddLine(canvas, 6, 11, 14, 11, brush, scale);
        AddEllipse(canvas, 12, 7, 2, 2, brush, scale, strokeOnly: false);
    }

    private static void DrawThermometer(Canvas canvas, Brush brush, double scale)
    {
        AddRoundedRect(canvas, 8, 3, 4, 11, brush, scale, strokeOnly: true);
        AddEllipse(canvas, 6.5, 12, 7, 7, brush, scale, strokeOnly: true);
        AddLine(canvas, 10, 6, 10, 13, brush, scale);
    }

    private static void DrawFan(Canvas canvas, Brush brush, double scale)
    {
        AddEllipse(canvas, 8.5, 8.5, 3, 3, brush, scale, strokeOnly: false);
        AddEllipse(canvas, 8.5, 2.5, 3, 7, brush, scale, strokeOnly: true);
        AddEllipse(canvas, 11, 9.5, 7, 3, brush, scale, strokeOnly: true);
        AddEllipse(canvas, 2, 9.5, 7, 3, brush, scale, strokeOnly: true);
    }

    private static void DrawMemory(Canvas canvas, Brush brush, double scale)
    {
        AddRoundedRect(canvas, 3, 6, 14, 8, brush, scale, strokeOnly: true);
        DrawMemoryCells(canvas, brush, scale, 5, 8);
        AddRect(canvas, 5, 14, 2, 2, brush, scale);
        AddRect(canvas, 9, 14, 2, 2, brush, scale);
        AddRect(canvas, 13, 14, 2, 2, brush, scale);
    }

    private static void DrawMemoryCells(Canvas canvas, Brush brush, double scale, double left, double top)
    {
        for (int index = 0; index < 3; index++)
        {
            AddRoundedRect(canvas, left + (index * 3), top, 2, 3, brush, scale, strokeOnly: false);
        }
    }

    private static void DrawBolt(Canvas canvas, Brush brush, double scale)
    {
        var polygon = new Polygon
        {
            Fill = brush,
            Points =
            {
                new global::Windows.Foundation.Point(11 * scale, 3 * scale),
                new global::Windows.Foundation.Point(6 * scale, 11 * scale),
                new global::Windows.Foundation.Point(10 * scale, 11 * scale),
                new global::Windows.Foundation.Point(8 * scale, 17 * scale),
                new global::Windows.Foundation.Point(14 * scale, 8 * scale),
                new global::Windows.Foundation.Point(10 * scale, 8 * scale),
            },
        };
        canvas.Children.Add(polygon);
    }

    private static void DrawGeneric(Canvas canvas, Brush brush, double scale)
    {
        AddRoundedRect(canvas, 5, 5, 10, 10, brush, scale, strokeOnly: true);
    }

    private static void AddRoundedRect(Canvas canvas, double left, double top, double width, double height, Brush brush, double scale, bool strokeOnly)
    {
        var rect = new Rectangle
        {
            Width = width * scale,
            Height = height * scale,
            RadiusX = 1.8 * scale,
            RadiusY = 1.8 * scale,
            StrokeThickness = strokeOnly ? 1.6 * scale : 0,
            Stroke = strokeOnly ? brush : null,
            Fill = strokeOnly ? null : brush,
        };
        Canvas.SetLeft(rect, left * scale);
        Canvas.SetTop(rect, top * scale);
        canvas.Children.Add(rect);
    }

    private static void AddRect(Canvas canvas, double left, double top, double width, double height, Brush brush, double scale)
    {
        var rect = new Rectangle
        {
            Width = width * scale,
            Height = height * scale,
            Fill = brush,
        };
        Canvas.SetLeft(rect, left * scale);
        Canvas.SetTop(rect, top * scale);
        canvas.Children.Add(rect);
    }

    private static void AddEllipse(Canvas canvas, double left, double top, double width, double height, Brush brush, double scale, bool strokeOnly)
    {
        var ellipse = new Ellipse
        {
            Width = width * scale,
            Height = height * scale,
            StrokeThickness = strokeOnly ? 1.5 * scale : 0,
            Stroke = strokeOnly ? brush : null,
            Fill = strokeOnly ? null : brush,
        };
        Canvas.SetLeft(ellipse, left * scale);
        Canvas.SetTop(ellipse, top * scale);
        canvas.Children.Add(ellipse);
    }

    private static void AddLine(Canvas canvas, double x1, double y1, double x2, double y2, Brush brush, double scale)
    {
        canvas.Children.Add(new Line
        {
            X1 = x1 * scale,
            Y1 = y1 * scale,
            X2 = x2 * scale,
            Y2 = y2 * scale,
            Stroke = brush,
            StrokeThickness = 1.5 * scale,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        });
    }
}
