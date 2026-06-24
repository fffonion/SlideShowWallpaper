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
        Assert.DoesNotContain("Package", text);
        Assert.DoesNotContain("Board Power", text);
        Assert.DoesNotContain("Fan", text);
    }

    [Fact]
    public void SelectSensors_WithNoSelection_UsesFirstEightSensors()
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
        Assert.Equal("sensor-0", selected[0].Id);
        Assert.Equal("sensor-7", selected[^1].Id);
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
}
