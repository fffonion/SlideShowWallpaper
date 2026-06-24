using System.Text.Json;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class HardwareOverlayTemplateService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public static HardwareOverlayTemplate FromConfig(HardwareMonitorConfig config)
    {
        return new HardwareOverlayTemplate
        {
            TemplateText = config.TemplateText,
            RefreshIntervalSeconds = config.RefreshIntervalSeconds,
            X = HardwareEditorLayoutService.QuantizeCoordinate(config.X, double.MaxValue),
            Y = HardwareEditorLayoutService.QuantizeCoordinate(config.Y, double.MaxValue),
            OverlayWidth = QuantizeLayoutLength(config.OverlayWidth, HardwareMonitorConfig.DefaultOverlayWidth, 1),
            OverlayHeight = QuantizeLayoutLength(config.OverlayHeight, HardwareMonitorConfig.DefaultOverlayHeight, 1),
            FontFamily = config.FontFamily,
            FontSize = config.FontSize,
            Opacity = config.Opacity,
            SelectedSensorIds = config.SelectedSensorIds.ToList(),
            BackgroundImagePath = config.BackgroundImagePath,
            BackgroundColor = config.BackgroundColor,
            Elements = CloneElements(config.Elements),
        };
    }

    public static void ApplyToConfig(HardwareOverlayTemplate template, HardwareMonitorConfig config)
    {
        config.TemplateText = string.IsNullOrWhiteSpace(template.TemplateText)
            ? HardwareMonitorConfig.DefaultTemplate
            : template.TemplateText;
        config.RefreshIntervalSeconds = Math.Max(1, template.RefreshIntervalSeconds);
        config.X = HardwareEditorLayoutService.QuantizeCoordinate(template.X, double.MaxValue);
        config.Y = HardwareEditorLayoutService.QuantizeCoordinate(template.Y, double.MaxValue);
        config.OverlayWidth = QuantizeLayoutLength(template.OverlayWidth, HardwareMonitorConfig.DefaultOverlayWidth, 1);
        config.OverlayHeight = QuantizeLayoutLength(template.OverlayHeight, HardwareMonitorConfig.DefaultOverlayHeight, 1);
        config.FontFamily = string.IsNullOrWhiteSpace(template.FontFamily) ? "Segoe UI" : template.FontFamily;
        config.FontSize = template.FontSize;
        config.Opacity = Math.Clamp(template.Opacity, 0.1, 1);
        config.SelectedSensorIds = template.SelectedSensorIds.ToList();
        config.BackgroundImagePath = template.BackgroundImagePath;
        config.BackgroundColor = template.BackgroundColor;
        config.Elements = CloneElements(template.Elements);
        config.SelectedElementId = config.Elements.FirstOrDefault()?.Id ?? string.Empty;
    }

    public static async Task ExportAsync(HardwareOverlayTemplate template, string path)
    {
        string json = JsonSerializer.Serialize(template, SerializerOptions);
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<HardwareOverlayTemplate> ImportAsync(string path)
    {
        string json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<HardwareOverlayTemplate>(json, SerializerOptions)
            ?? new HardwareOverlayTemplate();
    }

    private static List<HardwareOverlayElement> CloneElements(IEnumerable<HardwareOverlayElement> elements)
    {
        return elements
            .Select(element => new HardwareOverlayElement
            {
                Id = string.IsNullOrWhiteSpace(element.Id) ? Guid.NewGuid().ToString("N") : element.Id,
                Kind = element.Kind,
                SensorId = element.SensorId,
                Text = element.Text,
                ImagePath = element.ImagePath,
                X = HardwareEditorLayoutService.QuantizeCoordinate(element.X, double.MaxValue),
                Y = HardwareEditorLayoutService.QuantizeCoordinate(element.Y, double.MaxValue),
                Width = QuantizeLayoutLength(element.Width, 20, 20),
                Height = QuantizeLayoutLength(element.Height, 20, 20),
                FontFamily = element.FontFamily,
                FontSize = element.FontSize,
                Foreground = element.Foreground,
                Opacity = element.Opacity,
                DecimalPlaces = element.DecimalPlaces,
            })
            .ToList();
    }

    private static double QuantizeLayoutLength(double value, double defaultValue, double minimumValue)
    {
        double source = value > 0 ? value : defaultValue;
        return Math.Max(minimumValue, HardwareEditorLayoutService.QuantizeCoordinate(source, double.MaxValue));
    }
}
