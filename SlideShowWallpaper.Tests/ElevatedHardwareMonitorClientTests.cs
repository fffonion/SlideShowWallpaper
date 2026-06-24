using System.Text.Json;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class ElevatedHardwareMonitorClientTests
{
    [Fact]
    public void IsHelperMode_WithPipeArgument_ReturnsPipeName()
    {
        bool isHelper = ElevatedHardwareMonitorClient.IsHelperMode([
            LaunchOptions.HardwareMonitorHelperArgument,
            "pipe-name",
        ], out string pipeName);

        Assert.True(isHelper);
        Assert.Equal("pipe-name", pipeName);
    }

    [Fact]
    public void IsHelperMode_WithoutPipeArgument_ReturnsFalse()
    {
        bool isHelper = ElevatedHardwareMonitorClient.IsHelperMode([
            LaunchOptions.HardwareMonitorHelperArgument,
        ], out string pipeName);

        Assert.False(isHelper);
        Assert.Equal(string.Empty, pipeName);
    }

    [Fact]
    public void HardwareMonitorSnapshot_WithWebJsonOptions_RoundTripsSensorReadings()
    {
        var snapshot = new HardwareMonitorSnapshot(
            [
                new HardwareSensorReading("sensor1", "CPU", "Core", HardwareMetricKind.Temperature, HardwareMetricGroup.Cpu, 42, "C"),
            ],
            DateTimeOffset.Parse("2026-06-25T00:00:00+08:00"),
            IsElevated: true);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        string json = JsonSerializer.Serialize(snapshot, options);
        HardwareMonitorSnapshot? roundTrip = JsonSerializer.Deserialize<HardwareMonitorSnapshot>(json, options);

        Assert.NotNull(roundTrip);
        Assert.True(roundTrip.IsElevated);
        HardwareSensorReading reading = Assert.Single(roundTrip.Sensors);
        Assert.Equal("sensor1", reading.Id);
        Assert.Equal(HardwareMetricKind.Temperature, reading.Kind);
    }
}
