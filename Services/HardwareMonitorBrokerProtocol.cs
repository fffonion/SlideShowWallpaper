using System.Security.Cryptography;

namespace SlideShowWallpaper.Services;

public static class HardwareMonitorBrokerProtocol
{
    public const string BrokerArgument = "/hardware-broker";
    public const string PipeArgument = "/pipe";
    public const string ParentArgument = "/parent";
    public const string SnapshotCommand = "snapshot";
    public const string ShutdownCommand = "shutdown";

    public static string CreatePipeName()
    {
        return $"SlideShowWallpaper.Hardware.{Environment.ProcessId}.{RandomNumberGenerator.GetHexString(24)}";
    }

    public static bool TryGetOption(IReadOnlyList<string> arguments, string name, out string value)
    {
        for (int index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
            {
                value = arguments[index + 1];
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        value = string.Empty;
        return false;
    }
}

public sealed class HardwareMonitorBrokerRequest
{
    public string Command { get; set; } = HardwareMonitorBrokerProtocol.SnapshotCommand;

    public List<string> SensorIds { get; set; } = [];
}

public sealed class HardwareMonitorBrokerResponse
{
    public List<Models.HardwareSensorReading> Sensors { get; set; } = [];

    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.Now;

    public bool IsElevated { get; set; }

    public string Error { get; set; } = string.Empty;
}
