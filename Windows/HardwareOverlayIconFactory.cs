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
            case HardwareOverlayIconKind.Motherboard:
                DrawMotherboard(canvas, brush, scale);
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
        AddRoundedRect(canvas, 3.5, 4.5, 13, 11, brush, scale, strokeOnly: true);
        AddEllipse(canvas, 6, 6, 8, 8, brush, scale, strokeOnly: true);
        AddEllipse(canvas, 9, 9, 2, 2, brush, scale, strokeOnly: false);
        AddLine(canvas, 10, 10, 14, 7, brush, scale);
        AddEllipse(canvas, 13.4, 12.2, 1.6, 1.6, brush, scale, strokeOnly: false);
    }

    private static void DrawMotherboard(Canvas canvas, Brush brush, double scale)
    {
        AddRoundedRect(canvas, 3.5, 3.5, 13, 13, brush, scale, strokeOnly: true);
        AddRoundedRect(canvas, 7.5, 7.5, 5, 5, brush, scale, strokeOnly: true);
        AddLine(canvas, 5, 7, 9, 7, brush, scale);
        AddLine(canvas, 11, 13, 11, 16, brush, scale);
        AddLine(canvas, 13, 9, 16, 9, brush, scale);
        AddLine(canvas, 7, 11, 4, 11, brush, scale);
        AddCircuitNode(canvas, 5, 7, brush, scale);
        AddCircuitNode(canvas, 16, 9, brush, scale);
        AddCircuitNode(canvas, 4, 11, brush, scale);
        AddCircuitNode(canvas, 11, 16, brush, scale);
    }

    private static void DrawThermometer(Canvas canvas, Brush brush, double scale)
    {
        AddRoundedRect(canvas, 8, 3, 4, 11, brush, scale, strokeOnly: true);
        AddEllipse(canvas, 6.5, 12, 7, 7, brush, scale, strokeOnly: true);
        AddLine(canvas, 10, 6, 10, 13, brush, scale);
    }

    private static void DrawFan(Canvas canvas, Brush brush, double scale)
    {
        AddFanBlade(canvas, brush, scale, 0);
        AddFanBlade(canvas, brush, scale, 90);
        AddFanBlade(canvas, brush, scale, 180);
        AddFanBlade(canvas, brush, scale, 270);
        AddEllipse(canvas, 8.5, 8.5, 3, 3, brush, scale, strokeOnly: false);
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

    private static void AddFanBlade(Canvas canvas, Brush brush, double scale, double angle)
    {
        var geometry = new PathGeometry();
        var figure = new PathFigure
        {
            StartPoint = new global::Windows.Foundation.Point(10 * scale, 8.7 * scale),
            IsClosed = true,
            IsFilled = true,
        };
        figure.Segments.Add(new BezierSegment
        {
            Point1 = new global::Windows.Foundation.Point(11.5 * scale, 7 * scale),
            Point2 = new global::Windows.Foundation.Point(12.4 * scale, 5 * scale),
            Point3 = new global::Windows.Foundation.Point(11.5 * scale, 3.4 * scale),
        });
        figure.Segments.Add(new LineSegment
        {
            Point = new global::Windows.Foundation.Point(8.6 * scale, 4.1 * scale),
        });
        figure.Segments.Add(new BezierSegment
        {
            Point1 = new global::Windows.Foundation.Point(8.2 * scale, 5.8 * scale),
            Point2 = new global::Windows.Foundation.Point(8.6 * scale, 7.5 * scale),
            Point3 = new global::Windows.Foundation.Point(10 * scale, 8.7 * scale),
        });
        geometry.Figures.Add(figure);

        canvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry,
            Fill = brush,
            Opacity = 0.92,
            RenderTransform = new RotateTransform
            {
                Angle = angle,
                CenterX = 10 * scale,
                CenterY = 10 * scale,
            },
        });
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

    private static void AddCircuitNode(Canvas canvas, double centerX, double centerY, Brush brush, double scale)
    {
        AddEllipse(canvas, centerX - 0.8, centerY - 0.8, 1.6, 1.6, brush, scale, strokeOnly: false);
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
