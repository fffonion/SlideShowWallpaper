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
    public string DisplayName => string.IsNullOrWhiteSpace(HardwareName)
        ? SensorName
        : $"{HardwareName} / {SensorName}";
}

public sealed record HardwareMonitorSnapshot(IReadOnlyList<HardwareSensorReading> Sensors, DateTimeOffset CapturedAt, bool IsElevated = false);

public sealed record HardwareOverlayMetric(HardwareOverlayIconKind IconKind, string ValueText);

public sealed record HardwareOverlayElementState(
    string Id,
    HardwareOverlayElementKind Kind,
    string Text,
    string ImagePath,
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
