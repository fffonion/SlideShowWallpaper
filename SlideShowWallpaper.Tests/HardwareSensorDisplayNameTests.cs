using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Tests;

public sealed class HardwareSensorDisplayNameTests
{
    [Fact]
    public void Build_WithOrdinalOnlyFanName_AddsMetricName()
    {
        string displayName = HardwareSensorDisplayName.Build(
            "Nuvoton NCT6798D",
            "#2",
            HardwareMetricKind.FanRpm,
            HardwareMetricGroup.Motherboard,
            "/lpc/nct6798d/fan/1");

        Assert.Equal("Nuvoton NCT6798D / Fan #2", displayName);
    }

    [Fact]
    public void Build_WithOrdinalOnlyTemperatureName_AddsMetricAndHardwareName()
    {
        string displayName = HardwareSensorDisplayName.Build(
            "ITE IT8689E",
            "3",
            HardwareMetricKind.Temperature,
            HardwareMetricGroup.Motherboard,
            "/lpc/it8689e/temperature/2");

        Assert.Equal("ITE IT8689E / Temperature #3", displayName);
    }

    [Fact]
    public void Build_WithExistingReadableName_KeepsSensorName()
    {
        string displayName = HardwareSensorDisplayName.Build(
            "AMD Ryzen 9",
            "CPU Package",
            HardwareMetricKind.Temperature,
            HardwareMetricGroup.Cpu,
            "/amdcpu/0/temperature/0");

        Assert.Equal("AMD Ryzen 9 / CPU Package", displayName);
    }

    [Fact]
    public void Build_WithEmptySensorName_UsesIdentifierOrdinal()
    {
        string displayName = HardwareSensorDisplayName.Build(
            "GPU",
            string.Empty,
            HardwareMetricKind.Power,
            HardwareMetricGroup.Gpu,
            "/gpu-nvidia/0/power/1");

        Assert.Equal("GPU power #1", displayName);
    }
}
