using LibreHardwareMonitor.Hardware;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class HardwareMonitorReader : IDisposable
{
    private readonly object _syncRoot = new();
    private Computer? _computer;
    private CollectorProfile _collectorProfile = CollectorProfile.Full;
    private bool _disposed;

    public HardwareMonitorSnapshot GetSnapshot(IReadOnlyCollection<string>? sensorIds = null)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return new HardwareMonitorSnapshot([], DateTimeOffset.Now, CurrentProcessPrivilege.IsAdministrator());
            }

            HashSet<string>? requestedSensorIds = CreateRequestedSensorSet(sensorIds);
            CollectorProfile profile = CollectorProfile.FromSensorIds(requestedSensorIds);
            Computer computer = EnsureComputer(profile);
            var readings = new List<HardwareSensorReading>();
            var updated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IHardware hardware in computer.Hardware)
            {
                CollectHardware(readings, hardware, updated, requestedSensorIds);
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

    private Computer EnsureComputer(CollectorProfile profile)
    {
        if (_computer is not null && _collectorProfile.Equals(profile))
        {
            return _computer;
        }

        _computer?.Close();
        _collectorProfile = profile;
        _computer = new Computer
        {
            IsCpuEnabled = profile.Cpu,
            IsGpuEnabled = profile.Gpu,
            IsMemoryEnabled = profile.Memory,
            IsMotherboardEnabled = profile.Motherboard,
            IsStorageEnabled = profile.Storage,
        };
        _computer.Open();
        return _computer;
    }

    private static HashSet<string>? CreateRequestedSensorSet(IReadOnlyCollection<string>? sensorIds)
    {
        if (sensorIds is null || sensorIds.Count == 0)
        {
            return null;
        }

        var requested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string sensorId in sensorIds)
        {
            if (!string.IsNullOrWhiteSpace(sensorId))
            {
                requested.Add(sensorId);
            }
        }

        return requested.Count == 0 ? null : requested;
    }

    private static void CollectHardware(
        List<HardwareSensorReading> readings,
        IHardware hardware,
        HashSet<string> updated,
        IReadOnlySet<string>? requestedSensorIds)
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
                if (requestedSensorIds is null || requestedSensorIds.Contains(reading.Id))
                {
                    readings.Add(reading);
                }
            }
        }

        foreach (IHardware subHardware in hardware.SubHardware)
        {
            CollectHardware(readings, subHardware, updated, requestedSensorIds);
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

    private sealed record CollectorProfile(bool Cpu, bool Gpu, bool Memory, bool Motherboard, bool Storage)
    {
        public static CollectorProfile Full { get; } = new(Cpu: true, Gpu: true, Memory: true, Motherboard: true, Storage: true);

        public static CollectorProfile FromSensorIds(IReadOnlySet<string>? sensorIds)
        {
            if (sensorIds is null || sensorIds.Count == 0)
            {
                return Full;
            }

            bool cpu = false;
            bool gpu = false;
            bool memory = false;
            bool motherboard = false;
            bool storage = false;
            foreach (string sensorId in sensorIds)
            {
                CollectorGroup group = InferGroup(sensorId);
                switch (group)
                {
                    case CollectorGroup.Cpu:
                        cpu = true;
                        break;
                    case CollectorGroup.Gpu:
                        gpu = true;
                        break;
                    case CollectorGroup.Memory:
                        memory = true;
                        break;
                    case CollectorGroup.Motherboard:
                        motherboard = true;
                        break;
                    case CollectorGroup.Storage:
                        storage = true;
                        break;
                    default:
                        return Full;
                }
            }

            return new CollectorProfile(cpu, gpu, memory, motherboard, storage);
        }

        private static CollectorGroup InferGroup(string sensorId)
        {
            string id = sensorId.Trim();
            if (id.StartsWith("/intelcpu/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/amdcpu/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/genericcpu/", StringComparison.OrdinalIgnoreCase))
            {
                return CollectorGroup.Cpu;
            }

            if (id.StartsWith("/nvidiagpu/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/atigpu/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/adlgpu/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/intelgpu/", StringComparison.OrdinalIgnoreCase))
            {
                return CollectorGroup.Gpu;
            }

            if (id.StartsWith("/ram/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/memory/", StringComparison.OrdinalIgnoreCase))
            {
                return CollectorGroup.Memory;
            }

            if (id.StartsWith("/hdd/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/ssd/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/nvme/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/storage/", StringComparison.OrdinalIgnoreCase))
            {
                return CollectorGroup.Storage;
            }

            if (id.StartsWith("/lpc/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/mainboard/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/motherboard/", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith("/superio/", StringComparison.OrdinalIgnoreCase))
            {
                return CollectorGroup.Motherboard;
            }

            return CollectorGroup.Unknown;
        }
    }

    private enum CollectorGroup
    {
        Unknown,
        Cpu,
        Gpu,
        Memory,
        Motherboard,
        Storage
    }
}
