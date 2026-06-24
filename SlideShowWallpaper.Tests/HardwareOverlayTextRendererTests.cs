using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class HardwareOverlayTextRendererTests
{
    [Fact]
    public void Render_WithSelectedSensors_UsesTemplateAndSelectedOrder()
    {
        var config = new HardwareMonitorConfig
        {
            TemplateText = "Now\n{metrics}",
            SelectedSensorIds = ["gpu-power", "cpu-temp"],
        };
        var snapshot = new HardwareMonitorSnapshot(
            [
                new HardwareSensorReading("cpu-temp", "CPU", "Package", HardwareMetricKind.Temperature, HardwareMetricGroup.Cpu, 61.2, "C"),
                new HardwareSensorReading("gpu-power", "GPU", "Board Power", HardwareMetricKind.Power, HardwareMetricGroup.Gpu, 182.5, "W"),
                new HardwareSensorReading("fan", "Case", "Fan", HardwareMetricKind.FanRpm, HardwareMetricGroup.Motherboard, 1100, "RPM"),
            ],
            new DateTimeOffset(2026, 6, 24, 13, 30, 0, TimeSpan.Zero));

        string text = HardwareOverlayTextRenderer.Render(config, snapshot);

        Assert.Contains("Now", text);
        Assert.Contains("61.2", text);
        Assert.Contains("182.5 W", text);
        Assert.DoesNotContain("\ud83c", text);
        Assert.DoesNotContain("Package", text);
        Assert.DoesNotContain("Board Power", text);
        Assert.DoesNotContain("Fan", text);
    }

    [Fact]
    public void CreateMetrics_WithSelectedSensors_UsesVectorIconKindsAndSelectedOrder()
    {
        var config = new HardwareMonitorConfig
        {
            SelectedSensorIds = ["gpu-power", "cpu-temp"],
        };
        var snapshot = new HardwareMonitorSnapshot(
            [
                new HardwareSensorReading("cpu-temp", "CPU", "Package", HardwareMetricKind.Temperature, HardwareMetricGroup.Cpu, 61.2, "C"),
                new HardwareSensorReading("gpu-power", "GPU", "Board Power", HardwareMetricKind.Power, HardwareMetricGroup.Gpu, 182.5, "W"),
            ],
            DateTimeOffset.Now);

        IReadOnlyList<HardwareOverlayMetric> metrics = HardwareOverlayTextRenderer.CreateMetrics(config, snapshot);

        Assert.Equal(2, metrics.Count);
        Assert.Equal(HardwareOverlayIconKind.Cpu, metrics[0].IconKind);
        Assert.Equal("61.2 °C", metrics[0].ValueText);
        Assert.Equal(HardwareOverlayIconKind.Gpu, metrics[1].IconKind);
        Assert.Equal("182.5 W", metrics[1].ValueText);
    }

    [Fact]
    public void CreateElementStates_WithSensorElement_UsesCurrentSensorValue()
    {
        var config = new HardwareMonitorConfig
        {
            Elements =
            [
                new HardwareOverlayElement
                {
                    Id = "element1",
                    Kind = HardwareOverlayElementKind.Sensor,
                    SensorId = "gpu-power",
                    X = 20,
                    Y = 30,
                    Width = 120,
                    Height = 32,
                    FontFamily = "Segoe UI",
                    FontSize = 18,
                    Foreground = "#FFFFFFFF",
                    Opacity = 0.9,
                },
            ],
        };
        var snapshot = new HardwareMonitorSnapshot(
            [
                new HardwareSensorReading("gpu-power", "GPU", "Board Power", HardwareMetricKind.Power, HardwareMetricGroup.Gpu, 182.5, "W"),
            ],
            DateTimeOffset.Now);

        IReadOnlyList<HardwareOverlayElementState> elements = HardwareOverlayTextRenderer.CreateElementStates(config, snapshot);

        HardwareOverlayElementState element = Assert.Single(elements);
        Assert.Equal("element1", element.Id);
        Assert.Equal(HardwareOverlayElementKind.Sensor, element.Kind);
        Assert.Equal(HardwareOverlayIconKind.Gpu, element.IconKind);
        Assert.Equal("182.5 W", element.Text);
        Assert.Equal(20, element.X);
        Assert.Equal(30, element.Y);
        Assert.Equal(120, element.Width);
        Assert.Equal(32, element.Height);
        Assert.Equal(18, element.FontSize);
        Assert.Equal(0.9, element.Opacity);
    }

    [Fact]
    public void CreateElementStates_WithSensorDecimalPlaces_FormatsCurrentSensorValue()
    {
        var config = new HardwareMonitorConfig
        {
            Elements =
            [
                new HardwareOverlayElement
                {
                    Id = "element1",
                    Kind = HardwareOverlayElementKind.Sensor,
                    SensorId = "gpu-power",
                    DecimalPlaces = 2,
                },
            ],
        };
        var snapshot = new HardwareMonitorSnapshot(
            [
                new HardwareSensorReading("gpu-power", "GPU", "Board Power", HardwareMetricKind.Power, HardwareMetricGroup.Gpu, 182.456, "W"),
            ],
            DateTimeOffset.Now);

        HardwareOverlayElementState element = Assert.Single(HardwareOverlayTextRenderer.CreateElementStates(config, snapshot));

        Assert.Equal("182.46 W", element.Text);
    }

    [Fact]
    public void CreateElementStates_WithIntegerSensorDefault_UsesNoDecimalPlaces()
    {
        var config = new HardwareMonitorConfig
        {
            Elements =
            [
                new HardwareOverlayElement
                {
                    Id = "element1",
                    Kind = HardwareOverlayElementKind.Sensor,
                    SensorId = "fan",
                },
            ],
        };
        var snapshot = new HardwareMonitorSnapshot(
            [
                new HardwareSensorReading("fan", "Case", "Fan", HardwareMetricKind.FanRpm, HardwareMetricGroup.Motherboard, 2024.4, "RPM"),
            ],
            DateTimeOffset.Now);

        HardwareOverlayElementState element = Assert.Single(HardwareOverlayTextRenderer.CreateElementStates(config, snapshot));

        Assert.Equal("2024 RPM", element.Text);
    }

    [Fact]
    public void CreateElementStates_WithEmptyElementFont_UsesGlobalFontSettings()
    {
        var config = new HardwareMonitorConfig
        {
            FontFamily = "Cascadia Mono",
            FontSize = 21,
            Elements =
            [
                new HardwareOverlayElement
                {
                    Id = "element1",
                    Kind = HardwareOverlayElementKind.Text,
                    Text = "GPU",
                    FontFamily = string.Empty,
                    FontSize = 0,
                },
            ],
        };
        var snapshot = new HardwareMonitorSnapshot([], DateTimeOffset.Now);

        HardwareOverlayElementState element = Assert.Single(HardwareOverlayTextRenderer.CreateElementStates(config, snapshot));

        Assert.Equal("Cascadia Mono", element.FontFamily);
        Assert.Equal(21, element.FontSize);
    }

    [Fact]
    public void FormatReading_WithVramAvailableInMegabytes_DisplaysGigabytes()
    {
        var reading = new HardwareSensorReading(
            "gpu-memory-free",
            "GPU",
            "GPU Memory Free",
            HardwareMetricKind.VramAvailable,
            HardwareMetricGroup.Gpu,
            17470,
            "MB");

        string text = HardwareOverlayTextRenderer.FormatReading(reading);

        Assert.Equal("17.1 GB", text);
    }

    [Fact]
    public void FormatReading_WithNonIntegerSensorDefault_UsesOneDecimalPlace()
    {
        var reading = new HardwareSensorReading(
            "cpu-temp",
            "CPU",
            "Package",
            HardwareMetricKind.Temperature,
            HardwareMetricGroup.Cpu,
            61,
            "C");

        string text = HardwareOverlayTextRenderer.FormatReading(reading);

        Assert.Equal("61.0 \u00B0C", text);
    }

    [Fact]
    public void SelectSensors_WithNoSelection_UsesFirstEightDefaultSensors()
    {
        var config = new HardwareMonitorConfig();
        var snapshot = new HardwareMonitorSnapshot(
            Enumerable.Range(0, 12)
                .Select(index => new HardwareSensorReading(
                    $"sensor-{index}",
                    "CPU",
                    $"Sensor {index}",
                    HardwareMetricKind.Temperature,
                    HardwareMetricGroup.Cpu,
                    index,
                    "C"))
                .ToArray(),
            DateTimeOffset.Now);

        IReadOnlyList<HardwareSensorReading> selected = HardwareOverlayTextRenderer.SelectSensors(config, snapshot);

        Assert.Equal(8, selected.Count);
        Assert.Equal(
            HardwareOverlayTextRenderer.SelectDefaultSensors(snapshot).Take(8).Select(sensor => sensor.Id),
            selected.Select(sensor => sensor.Id));
    }

    [Fact]
    public void SelectSensors_WithVirtualMemoryAvailable_ExcludesIt()
    {
        var config = new HardwareMonitorConfig
        {
            SelectedSensorIds = ["virtual-memory", "physical-memory"],
        };
        var snapshot = new HardwareMonitorSnapshot(
            [
                new HardwareSensorReading("virtual-memory", "Memory", "Virtual Memory Available", HardwareMetricKind.MemoryAvailable, HardwareMetricGroup.Memory, 128, "GB"),
                new HardwareSensorReading("physical-memory", "Memory", "Memory Available", HardwareMetricKind.MemoryAvailable, HardwareMetricGroup.Memory, 32, "GB"),
            ],
            DateTimeOffset.Now);

        IReadOnlyList<HardwareSensorReading> selected = HardwareOverlayTextRenderer.SelectSensors(config, snapshot);

        HardwareSensorReading reading = Assert.Single(selected);
        Assert.Equal("physical-memory", reading.Id);
    }

    [Fact]
    public void SelectDefaultSensors_WithMixedSensors_PrioritizesTemperatureFanMemoryAndPower()
    {
        var snapshot = new HardwareMonitorSnapshot(
            [
                new HardwareSensorReading("gpu-power", "GPU", "GPU Package", HardwareMetricKind.Power, HardwareMetricGroup.Gpu, 180, "W"),
                new HardwareSensorReading("memory", "Memory", "Memory Available", HardwareMetricKind.MemoryAvailable, HardwareMetricGroup.Memory, 32, "GB"),
                new HardwareSensorReading("storage-temp", "NVMe", "Temperature", HardwareMetricKind.Temperature, HardwareMetricGroup.Storage, 45, "C"),
                new HardwareSensorReading("gpu-temp", "GPU", "GPU Core", HardwareMetricKind.Temperature, HardwareMetricGroup.Gpu, 60, "C"),
                new HardwareSensorReading("fan", "Mainboard", "Fan #1", HardwareMetricKind.FanRpm, HardwareMetricGroup.Motherboard, 1100, "RPM"),
                new HardwareSensorReading("cpu-temp", "CPU", "CPU Package", HardwareMetricKind.Temperature, HardwareMetricGroup.Cpu, 55, "C"),
                new HardwareSensorReading("vram", "GPU", "GPU Memory Free", HardwareMetricKind.VramAvailable, HardwareMetricGroup.Gpu, 12000, "MB"),
                new HardwareSensorReading("cpu-power", "CPU", "CPU Package", HardwareMetricKind.Power, HardwareMetricGroup.Cpu, 95, "W"),
            ],
            DateTimeOffset.Now);

        IReadOnlyList<HardwareSensorReading> selected = HardwareOverlayTextRenderer.SelectDefaultSensors(snapshot);

        Assert.Equal(
            ["cpu-temp", "gpu-temp", "storage-temp", "fan", "memory", "vram", "cpu-power", "gpu-power"],
            selected.Select(sensor => sensor.Id).ToArray());
    }
}
