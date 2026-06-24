using LibreHardwareMonitor.Hardware;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class HardwareMonitorReader : IDisposable
{
    private readonly object _syncRoot = new();
    private Computer? _computer;
    private bool _disposed;

    public HardwareMonitorSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return new HardwareMonitorSnapshot([], DateTimeOffset.Now, CurrentProcessPrivilege.IsAdministrator());
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
