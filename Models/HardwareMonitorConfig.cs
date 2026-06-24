namespace SlideShowWallpaper.Models;

public enum HardwareMetricKind
{
    Temperature,
    FanRpm,
    MemoryAvailable,
    VramAvailable,
    Power
}

public enum HardwareMetricGroup
{
    Cpu,
    Gpu,
    Storage,
    Memory,
    Motherboard,
    Other
}

public enum HardwareOverlayIconKind
{
    Cpu,
    Gpu,
    Storage,
    Temperature,
    Fan,
    Memory,
    Vram,
    Power,
    Generic
}

public enum HardwareOverlayElementKind
{
    Sensor,
    Text,
    Image
}

public sealed class HardwareMonitorConfig
{
    public const string DefaultTemplate = "{metrics}";

    public const int DefaultRefreshIntervalSeconds = 5;

    public bool IsEnabled { get; set; }

    public int RefreshIntervalSeconds { get; set; } = DefaultRefreshIntervalSeconds;

    public string TargetMonitorId { get; set; } = string.Empty;

    public string TemplateText { get; set; } = DefaultTemplate;

    public double X { get; set; } = 24;

    public double Y { get; set; } = 24;

    public double FontSize { get; set; } = 16;

    public double Opacity { get; set; } = 0.88;

    public List<string> SelectedSensorIds { get; set; } = [];

    public string BackgroundImagePath { get; set; } = string.Empty;

    public string SelectedElementId { get; set; } = string.Empty;

    public List<HardwareOverlayElement> Elements { get; set; } = [];
}

public sealed class HardwareOverlayElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public HardwareOverlayElementKind Kind { get; set; } = HardwareOverlayElementKind.Sensor;

    public string SensorId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string ImagePath { get; set; } = string.Empty;

    public double X { get; set; } = 24;

    public double Y { get; set; } = 24;

    public double Width { get; set; } = 160;

    public double Height { get; set; } = 40;

    public string FontFamily { get; set; } = "Segoe UI";

    public double FontSize { get; set; } = 16;

    public string Foreground { get; set; } = "#FFFFFFFF";

    public double Opacity { get; set; } = 1;
}

public sealed class HardwareOverlayTemplate
{
    public string Name { get; set; } = "Hardware Overlay";

    public int RefreshIntervalSeconds { get; set; } = HardwareMonitorConfig.DefaultRefreshIntervalSeconds;

    public string TemplateText { get; set; } = HardwareMonitorConfig.DefaultTemplate;

    public double X { get; set; } = 24;

    public double Y { get; set; } = 24;

    public double FontSize { get; set; } = 16;

    public double Opacity { get; set; } = 0.88;

    public List<string> SelectedSensorIds { get; set; } = [];

    public string BackgroundImagePath { get; set; } = string.Empty;

    public List<HardwareOverlayElement> Elements { get; set; } = [];
}

public sealed record HardwareSensorReading(
    string Id,
    string HardwareName,
    string SensorName,
    HardwareMetricKind Kind,
    HardwareMetricGroup Group,
    double Value,
    string Unit)
{
    public string DisplayName => HardwareSensorDisplayName.Build(HardwareName, SensorName, Kind, Group, Id);
}

public static class HardwareSensorDisplayName
{
    public static string Build(
        string hardwareName,
        string sensorName,
        HardwareMetricKind kind,
        HardwareMetricGroup group,
        string id)
    {
        string normalizedSensorName = NormalizeWhitespace(sensorName);
        string readableSensorName = BuildReadableSensorName(normalizedSensorName, kind, group, id);
        string normalizedHardwareName = NormalizeWhitespace(hardwareName);
        if (string.IsNullOrWhiteSpace(normalizedHardwareName))
        {
            return readableSensorName;
        }

        if (readableSensorName.Contains(normalizedHardwareName, StringComparison.OrdinalIgnoreCase))
        {
            return readableSensorName;
        }

        return $"{normalizedHardwareName} / {readableSensorName}";
    }

    private static string BuildReadableSensorName(
        string sensorName,
        HardwareMetricKind kind,
        HardwareMetricGroup group,
        string id)
    {
        if (!IsOrdinalOnlyName(sensorName))
        {
            return string.IsNullOrWhiteSpace(sensorName) ? GetMetricName(kind, group) : sensorName;
        }

        string ordinal = ExtractOrdinal(sensorName) ?? ExtractIdentifierOrdinal(id);
        string metricName = GetMetricName(kind, group);
        return string.IsNullOrWhiteSpace(ordinal)
            ? metricName
            : $"{metricName} {ordinal}";
    }

    private static string GetMetricName(HardwareMetricKind kind, HardwareMetricGroup group)
    {
        return kind switch
        {
            HardwareMetricKind.Temperature => group switch
            {
                HardwareMetricGroup.Cpu => "CPU temperature",
                HardwareMetricGroup.Gpu => "GPU temperature",
                HardwareMetricGroup.Storage => "Drive temperature",
                _ => "Temperature",
            },
            HardwareMetricKind.FanRpm => "Fan",
            HardwareMetricKind.MemoryAvailable => "Memory available",
            HardwareMetricKind.VramAvailable => "VRAM available",
            HardwareMetricKind.Power => group == HardwareMetricGroup.Gpu ? "GPU power" : "CPU power",
            _ => "Sensor",
        };
    }

    private static bool IsOrdinalOnlyName(string sensorName)
    {
        if (string.IsNullOrWhiteSpace(sensorName))
        {
            return true;
        }

        string value = sensorName.Trim();
        if (value.StartsWith('#') && value.Skip(1).All(char.IsDigit))
        {
            return true;
        }

        return value.All(char.IsDigit);
    }

    private static string? ExtractOrdinal(string sensorName)
    {
        string value = sensorName.Trim();
        if (value.Length == 0)
        {
            return null;
        }

        return value.StartsWith("#", StringComparison.Ordinal)
            ? value
            : $"#{value}";
    }

    private static string ExtractIdentifierOrdinal(string id)
    {
        string[] parts = id.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? lastNumber = parts.LastOrDefault(part => part.All(char.IsDigit));
        return string.IsNullOrWhiteSpace(lastNumber) ? string.Empty : $"#{lastNumber}";
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed record HardwareMonitorSnapshot(IReadOnlyList<HardwareSensorReading> Sensors, DateTimeOffset CapturedAt, bool IsElevated = false);

public sealed record HardwareOverlayMetric(HardwareOverlayIconKind IconKind, string ValueText);

public sealed record HardwareOverlayElementState(
    string Id,
    HardwareOverlayElementKind Kind,
    string Text,
    string ImagePath,
    HardwareOverlayIconKind IconKind,
    double X,
    double Y,
    double Width,
    double Height,
    string FontFamily,
    double FontSize,
    string Foreground,
    double Opacity);

public sealed record HardwareOverlayState(
    bool IsVisible,
    string Text,
    IReadOnlyList<HardwareOverlayMetric> Metrics,
    double X,
    double Y,
    double FontSize,
    double Opacity)
{
    public string BackgroundImagePath { get; init; } = string.Empty;

    public IReadOnlyList<HardwareOverlayElementState> Elements { get; init; } = [];
}

public sealed record HardwareOverlayMovedEventArgs(double X, double Y);
