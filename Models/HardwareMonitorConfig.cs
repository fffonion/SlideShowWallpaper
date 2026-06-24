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
    public string DisplayName => string.IsNullOrWhiteSpace(HardwareName)
        ? SensorName
        : $"{HardwareName} / {SensorName}";
}

public sealed record HardwareMonitorSnapshot(IReadOnlyList<HardwareSensorReading> Sensors, DateTimeOffset CapturedAt);

public sealed record HardwareOverlayState(bool IsVisible, string Text, double X, double Y, double FontSize, double Opacity);
