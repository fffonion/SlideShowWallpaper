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
            X = config.X,
            Y = config.Y,
            FontSize = config.FontSize,
            Opacity = config.Opacity,
            SelectedSensorIds = config.SelectedSensorIds.ToList(),
        };
    }

    public static void ApplyToConfig(HardwareOverlayTemplate template, HardwareMonitorConfig config)
    {
        config.TemplateText = string.IsNullOrWhiteSpace(template.TemplateText)
            ? HardwareMonitorConfig.DefaultTemplate
            : template.TemplateText;
        config.RefreshIntervalSeconds = Math.Max(1, template.RefreshIntervalSeconds);
        config.X = template.X;
        config.Y = template.Y;
        config.FontSize = template.FontSize;
        config.Opacity = Math.Clamp(template.Opacity, 0.1, 1);
        config.SelectedSensorIds = template.SelectedSensorIds.ToList();
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
}
