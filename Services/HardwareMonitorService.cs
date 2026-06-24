using System.Globalization;
using LibreHardwareMonitor.Hardware;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class HardwareMonitorService : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly bool _useElevatedHelper;
    private Computer? _computer;
    private ElevatedHardwareMonitorClient? _elevatedClient;
    private bool _disposed;

    public HardwareMonitorService(bool useElevatedHelper = true)
    {
        _useElevatedHelper = useElevatedHelper;
    }

    public HardwareMonitorSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return new HardwareMonitorSnapshot([], DateTimeOffset.Now, CurrentProcessPrivilege.IsAdministrator());
            }

            if (_useElevatedHelper && _elevatedClient?.GetSnapshot() is HardwareMonitorSnapshot elevatedSnapshot)
            {
                return elevatedSnapshot;
            }

            Computer computer = EnsureComputer();
            var readings = new List<HardwareSensorReading>();
            var updated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IHardware hardware in computer.Hardware)
            {
                CollectHardware(readings, hardware, updated);
            }

            return new HardwareMonitorSnapshot(readings, DateTimeOffset.Now, CurrentProcessPrivilege.IsAdministrator());
        }
    }

    public bool TryStartElevatedReader()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return false;
            }

            if (CurrentProcessPrivilege.IsAdministrator())
            {
                return true;
            }

            _elevatedClient ??= new ElevatedHardwareMonitorClient();
            return _elevatedClient.Start();
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _computer?.Close();
            _computer = null;
            _elevatedClient?.Dispose();
            _elevatedClient = null;
            _disposed = true;
        }
    }

    private Computer EnsureComputer()
    {
        if (_computer is not null)
        {
            return _computer;
        }

        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsPsuEnabled = true,
            IsStorageEnabled = true,
        };
        _computer.Open();
        return _computer;
    }

    private static void CollectHardware(List<HardwareSensorReading> readings, IHardware hardware, HashSet<string> updated)
    {
        UpdateHardwareOnce(hardware, updated);
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (!sensor.Value.HasValue)
            {
                continue;
            }

            HardwareSensorReading? reading = CreateReading(hardware, sensor, sensor.Value.Value);
            if (reading is not null)
            {
                readings.Add(reading);
            }
        }

        foreach (IHardware subHardware in hardware.SubHardware)
        {
            CollectHardware(readings, subHardware, updated);
        }
    }

    private static void UpdateHardwareOnce(IHardware hardware, HashSet<string> updated)
    {
        string id = hardware.Identifier.ToString();
        if (updated.Add(id))
        {
            hardware.Update();
        }
    }

    private static HardwareSensorReading? CreateReading(IHardware hardware, ISensor sensor, double value)
    {
        HardwareMetricGroup group = ToGroup(hardware.HardwareType);
        HardwareMetricKind? kind = sensor.SensorType switch
        {
            SensorType.Temperature when group is HardwareMetricGroup.Cpu or HardwareMetricGroup.Gpu or HardwareMetricGroup.Storage => HardwareMetricKind.Temperature,
            SensorType.Temperature when group is HardwareMetricGroup.Motherboard => HardwareMetricKind.Temperature,
            SensorType.Fan => HardwareMetricKind.FanRpm,
            SensorType.Power when group is HardwareMetricGroup.Cpu or HardwareMetricGroup.Gpu => HardwareMetricKind.Power,
            _ => TryCreateMemoryKind(group, sensor.Name),
        };
        if (kind is null)
        {
            return null;
        }

        string unit = kind.Value switch
        {
            HardwareMetricKind.Temperature => "C",
            HardwareMetricKind.FanRpm => "RPM",
            HardwareMetricKind.Power => "W",
            HardwareMetricKind.VramAvailable => "MB",
            _ => "GB",
        };
        return new HardwareSensorReading(
            sensor.Identifier.ToString(),
            hardware.Name,
            sensor.Name,
            kind.Value,
            group,
            value,
            unit);
    }

    private static HardwareMetricKind? TryCreateMemoryKind(HardwareMetricGroup group, string sensorName)
    {
        if (group == HardwareMetricGroup.Memory
            && sensorName.Contains("Available", StringComparison.OrdinalIgnoreCase)
            && !sensorName.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
        {
            return HardwareMetricKind.MemoryAvailable;
        }

        if (group == HardwareMetricGroup.Gpu
            && (sensorName.Contains("Memory Free", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("Memory Available", StringComparison.OrdinalIgnoreCase)
                || sensorName.Contains("VRAM Free", StringComparison.OrdinalIgnoreCase)))
        {
            return HardwareMetricKind.VramAvailable;
        }

        return null;
    }

    private static HardwareMetricGroup ToGroup(HardwareType hardwareType)
    {
        return hardwareType switch
        {
            HardwareType.Cpu => HardwareMetricGroup.Cpu,
            HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia => HardwareMetricGroup.Gpu,
            HardwareType.Storage => HardwareMetricGroup.Storage,
            HardwareType.Memory => HardwareMetricGroup.Memory,
            HardwareType.Motherboard or HardwareType.SuperIO => HardwareMetricGroup.Motherboard,
            _ => HardwareMetricGroup.Other,
        };
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
        (double displayValue, string displayUnit) = NormalizeDisplayUnit(reading);
        string value = reading.Kind switch
        {
            HardwareMetricKind.FanRpm => displayValue.ToString("0", CultureInfo.CurrentCulture),
            HardwareMetricKind.MemoryAvailable or HardwareMetricKind.VramAvailable => displayValue.ToString("0.0", CultureInfo.CurrentCulture),
            _ => displayValue.ToString("0.#", CultureInfo.CurrentCulture),
        };

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
            HardwareOverlayElementKind.Sensor when sensorReading is not null => FormatReading(sensorReading),
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
