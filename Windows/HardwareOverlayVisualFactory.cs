using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Windows;

internal static class HardwareOverlayVisualFactory
{
    public static TextBlock CreateText(string text, string fontFamily, double fontSize)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily(NormalizeFontFamily(fontFamily)),
            FontSize = Math.Max(8, fontSize),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.White),
            TextWrapping = TextWrapping.NoWrap,
        };
    }

    public static FrameworkElement CreateMetricRow(HardwareOverlayMetric metric, string fontFamily, double fontSize)
    {
        double resolvedFontSize = Math.Max(8, fontSize);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(HardwareOverlayIconFactory.CreateIcon(metric.IconKind, Math.Max(17, resolvedFontSize + 2)));
        row.Children.Add(CreateText(metric.ValueText, fontFamily, resolvedFontSize));
        return row;
    }

    public static FrameworkElement CreateElement(HardwareOverlayElementState element)
    {
        if (element.Kind == HardwareOverlayElementKind.Image && TryCreateBitmapImage(element.ImagePath, out BitmapImage? bitmap))
        {
            return new Image
            {
                Source = bitmap,
                Width = element.Width,
                Height = element.Height,
                Stretch = Stretch.UniformToFill,
                Opacity = element.Opacity,
            };
        }

        if (element.Kind == HardwareOverlayElementKind.Sensor)
        {
            return CreateSensorElement(element);
        }

        return new TextBlock
        {
            Text = element.Text,
            FontFamily = new FontFamily(NormalizeFontFamily(element.FontFamily)),
            FontSize = NormalizeFontSize(element.FontSize),
            Foreground = CreateBrush(element.Foreground),
            Width = element.Width,
            Height = element.Height,
            TextWrapping = TextWrapping.WrapWholeWords,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = element.Opacity,
        };
    }

    public static SolidColorBrush CreateBrush(string value)
    {
        if (TryParseColor(value, out global::Windows.UI.Color color))
        {
            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(Microsoft.UI.Colors.White);
    }

    private static FrameworkElement CreateSensorElement(HardwareOverlayElementState element)
    {
        double fontSize = NormalizeFontSize(element.FontSize);
        Brush brush = CreateBrush(element.Foreground);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Width = element.Width,
            Height = element.Height,
            Opacity = element.Opacity,
            VerticalAlignment = VerticalAlignment.Center,
        };
        double iconSize = Math.Max(17, fontSize + 2);
        if (TryCreateBitmapImage(element.ImagePath, out BitmapImage? bitmap))
        {
            row.Children.Add(new Image
            {
                Source = bitmap,
                Width = iconSize,
                Height = iconSize,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        else
        {
            row.Children.Add(HardwareOverlayIconFactory.CreateIcon(element.IconKind, iconSize, brush));
        }

        row.Children.Add(new TextBlock
        {
            Text = element.Text,
            FontFamily = new FontFamily(NormalizeFontFamily(element.FontFamily)),
            FontSize = fontSize,
            Foreground = brush,
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    public static bool TryCreateBitmapImage(string path, out BitmapImage? bitmap)
    {
        bitmap = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            bitmap = new BitmapImage(new Uri(path));
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            bitmap = null;
            return false;
        }
    }

    private static string NormalizeFontFamily(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Segoe UI" : value;
    }

    private static double NormalizeFontSize(double value)
    {
        return Math.Max(8, value > 0 ? value : 16);
    }

    private static bool TryParseColor(string value, out global::Windows.UI.Color color)
    {
        color = Microsoft.UI.Colors.White;
        string hex = value.Trim().TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (hex.Length != 8 || !uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint argb))
        {
            return false;
        }

        color = Microsoft.UI.ColorHelper.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));
        return true;
    }
}
