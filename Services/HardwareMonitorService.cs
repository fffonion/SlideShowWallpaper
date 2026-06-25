using System.Globalization;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class HardwareMonitorService : IDisposable
{
    private readonly HardwareMonitorBrokerClient _brokerClient;

    public HardwareMonitorService()
        : this(new HardwareMonitorBrokerClient())
    {
    }

    internal HardwareMonitorService(HardwareMonitorBrokerClient brokerClient)
    {
        _brokerClient = brokerClient;
        _brokerClient.BrokerProcessStarted += BrokerClient_BrokerProcessStarted;
    }

    public event EventHandler? BrokerProcessStarted;

    public void UseBrokerPipe(string brokerPipeName)
    {
        _brokerClient.UseBrokerPipe(brokerPipeName);
    }

    public void SetBrokerElevation(bool startElevated)
    {
        _brokerClient.SetStartElevated(startElevated);
    }

    public void StartBroker()
    {
        _brokerClient.StartBroker();
    }

    public void StopBroker()
    {
        _brokerClient.StopBroker();
    }

    public HardwareMonitorSnapshot GetSnapshot(HardwareMonitorConfig? config = null)
    {
        return _brokerClient.GetSnapshot(config);
    }

    public void Dispose()
    {
        _brokerClient.BrokerProcessStarted -= BrokerClient_BrokerProcessStarted;
        _brokerClient.Dispose();
    }

    private void BrokerClient_BrokerProcessStarted(object? sender, EventArgs args)
    {
        BrokerProcessStarted?.Invoke(this, args);
    }
}

public static class HardwareOverlayTextRenderer
{
    public static string Render(HardwareMonitorConfig config, HardwareMonitorSnapshot snapshot)
    {
        string metrics = string.Join(Environment.NewLine, CreateMetrics(config, snapshot).Select(metric => metric.ValueText));
        string template = string.IsNullOrWhiteSpace(config.TemplateText)
            ? HardwareMonitorConfig.DefaultTemplate
            : config.TemplateText;
        return template
            .Replace("{metrics}", metrics, StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", snapshot.CapturedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture), StringComparison.OrdinalIgnoreCase);
    }

    public static string RenderStaticText(HardwareMonitorConfig config, HardwareMonitorSnapshot snapshot)
    {
        string template = string.IsNullOrWhiteSpace(config.TemplateText)
            ? HardwareMonitorConfig.DefaultTemplate
            : config.TemplateText;
        return template
            .Replace("{metrics}", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", snapshot.CapturedAt.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture), StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    public static IReadOnlyList<HardwareOverlayMetric> CreateMetrics(HardwareMonitorConfig config, HardwareMonitorSnapshot snapshot)
    {
        return SelectSensors(config, snapshot)
            .Select(reading => new HardwareOverlayMetric(GetIconKind(reading), FormatReading(reading)))
            .ToArray();
    }

    public static IReadOnlyList<HardwareOverlayElementState> CreateElementStates(HardwareMonitorConfig config, HardwareMonitorSnapshot snapshot)
    {
        if (config.Elements.Count == 0)
        {
            return [];
        }

        Dictionary<string, HardwareSensorReading> readings = snapshot.Sensors
            .Where(IsVisibleReading)
            .GroupBy(sensor => sensor.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        string fallbackFontFamily = string.IsNullOrWhiteSpace(config.FontFamily) ? "Segoe UI" : config.FontFamily;
        double fallbackFontSize = Math.Max(8, config.FontSize);
        return config.Elements
            .Select(element => CreateElementState(element, readings, fallbackFontFamily, fallbackFontSize))
            .ToArray();
    }

    public static IReadOnlyList<HardwareSensorReading> SelectSensors(HardwareMonitorConfig config, HardwareMonitorSnapshot snapshot)
    {
        IEnumerable<HardwareSensorReading> visibleSensors = snapshot.Sensors.Where(IsVisibleReading);
        if (config.SelectedSensorIds.Count == 0)
        {
            return SelectDefaultSensors(snapshot).Take(8).ToArray();
        }

        HashSet<string> selectedIds = config.SelectedSensorIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return visibleSensors.Where(sensor => selectedIds.Contains(sensor.Id)).ToArray();
    }

    public static IReadOnlyList<HardwareSensorReading> SelectDefaultSensors(HardwareMonitorSnapshot snapshot)
    {
        return snapshot.Sensors
            .Where(IsVisibleReading)
            .OrderBy(GetDefaultSensorPriority)
            .ThenBy(sensor => sensor.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static string FormatReading(HardwareSensorReading reading)
    {
        return FormatReading(reading, GetDefaultDecimalPlaces(reading));
    }

    public static string FormatReading(HardwareSensorReading reading, int decimalPlaces)
    {
        (double displayValue, string displayUnit) = NormalizeDisplayUnit(reading);
        string value = displayValue.ToString(CreateDecimalFormat(decimalPlaces), CultureInfo.CurrentCulture);

        return $"{value} {displayUnit}";
    }

    public static HardwareOverlayIconKind GetIconKind(HardwareSensorReading reading)
    {
        return reading.Kind switch
        {
            HardwareMetricKind.Temperature => reading.Group switch
            {
                HardwareMetricGroup.Cpu => HardwareOverlayIconKind.Cpu,
                HardwareMetricGroup.Gpu => HardwareOverlayIconKind.Gpu,
                HardwareMetricGroup.Storage => HardwareOverlayIconKind.Storage,
                HardwareMetricGroup.Motherboard => HardwareOverlayIconKind.Motherboard,
                _ => HardwareOverlayIconKind.Temperature,
            },
            HardwareMetricKind.FanRpm => HardwareOverlayIconKind.Fan,
            HardwareMetricKind.MemoryAvailable => HardwareOverlayIconKind.Memory,
            HardwareMetricKind.VramAvailable => HardwareOverlayIconKind.Vram,
            HardwareMetricKind.Power => reading.Group == HardwareMetricGroup.Gpu
                ? HardwareOverlayIconKind.Gpu
                : HardwareOverlayIconKind.Cpu,
            _ => HardwareOverlayIconKind.Generic,
        };
    }

    private static HardwareOverlayElementState CreateElementState(
        HardwareOverlayElement element,
        IReadOnlyDictionary<string, HardwareSensorReading> readings,
        string fallbackFontFamily,
        double fallbackFontSize)
    {
        readings.TryGetValue(element.SensorId, out HardwareSensorReading? sensorReading);
        string text = element.Kind switch
        {
            HardwareOverlayElementKind.Sensor when sensorReading is not null => FormatReading(sensorReading, ResolveDecimalPlaces(element, sensorReading)),
            HardwareOverlayElementKind.Sensor => string.IsNullOrWhiteSpace(element.Text) ? element.SensorId : element.Text,
            HardwareOverlayElementKind.Text => element.Text,
            _ => string.Empty,
        };
        HardwareOverlayIconKind iconKind = sensorReading is null
            ? HardwareOverlayIconKind.Generic
            : GetIconKind(sensorReading);
        return new HardwareOverlayElementState(
            element.Id,
            element.Kind,
            text,
            element.ImagePath,
            iconKind,
            Math.Max(0, element.X),
            Math.Max(0, element.Y),
            Math.Max(20, element.Width),
            Math.Max(20, element.Height),
            string.IsNullOrWhiteSpace(element.FontFamily) ? fallbackFontFamily : element.FontFamily,
            element.FontSize > 0 ? Math.Max(8, element.FontSize) : fallbackFontSize,
            string.IsNullOrWhiteSpace(element.Foreground) ? "#FFFFFFFF" : element.Foreground,
            Math.Clamp(element.Opacity, 0.05, 1));
    }

    private static bool IsVisibleReading(HardwareSensorReading reading)
    {
        return !string.Equals(reading.SensorName, "Virtual Memory Available", StringComparison.OrdinalIgnoreCase);
    }

    private static int ResolveDecimalPlaces(HardwareOverlayElement element, HardwareSensorReading reading)
    {
        return element.DecimalPlaces >= 0
            ? Math.Clamp(element.DecimalPlaces, 0, 6)
            : GetDefaultDecimalPlaces(reading);
    }

    private static int GetDefaultDecimalPlaces(HardwareSensorReading reading)
    {
        return reading.Kind == HardwareMetricKind.FanRpm ? 0 : 1;
    }

    private static string CreateDecimalFormat(int decimalPlaces)
    {
        int clamped = Math.Clamp(decimalPlaces, 0, 6);
        return clamped == 0 ? "0" : $"0.{new string('0', clamped)}";
    }

    private static int GetDefaultSensorPriority(HardwareSensorReading reading)
    {
        return (reading.Group, reading.Kind) switch
        {
            (HardwareMetricGroup.Cpu, HardwareMetricKind.Temperature) => 0,
            (HardwareMetricGroup.Gpu, HardwareMetricKind.Temperature) => 1,
            (HardwareMetricGroup.Storage, HardwareMetricKind.Temperature) => 2,
            (_, HardwareMetricKind.FanRpm) => 3,
            (HardwareMetricGroup.Memory, HardwareMetricKind.MemoryAvailable) => 4,
            (HardwareMetricGroup.Gpu, HardwareMetricKind.VramAvailable) => 5,
            (HardwareMetricGroup.Cpu, HardwareMetricKind.Power) => 6,
            (HardwareMetricGroup.Gpu, HardwareMetricKind.Power) => 7,
            _ => 100,
        };
    }

    private static (double Value, string Unit) NormalizeDisplayUnit(HardwareSensorReading reading)
    {
        if (reading.Unit == "C")
        {
            return (reading.Value, "\u00B0C");
        }

        if (reading.Kind is not HardwareMetricKind.MemoryAvailable and not HardwareMetricKind.VramAvailable)
        {
            return (reading.Value, reading.Unit);
        }

        return reading.Unit.ToUpperInvariant() switch
        {
            "B" => (reading.Value / 1024 / 1024 / 1024, "GB"),
            "KB" => (reading.Value / 1024 / 1024, "GB"),
            "MB" => (reading.Value / 1024, "GB"),
            _ => (reading.Value, reading.Unit),
        };
    }
}
