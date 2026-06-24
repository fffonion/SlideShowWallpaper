using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class HardwareMonitorBrokerClient : IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _syncRoot = new();
    private readonly string _pipeName = HardwareMonitorBrokerProtocol.CreatePipeName();
    private Process? _brokerProcess;
    private bool _disposed;

    public event EventHandler? BrokerProcessStarted;

    public HardwareMonitorSnapshot GetSnapshot()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return EmptySnapshot();
            }

            return TryGetSnapshot(restartBroker: false) ?? TryGetSnapshot(restartBroker: true) ?? EmptySnapshot();
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

            TrySendShutdown();
            _brokerProcess?.Dispose();
            _brokerProcess = null;
            _disposed = true;
        }
    }

    private HardwareMonitorSnapshot? TryGetSnapshot(bool restartBroker)
    {
        try
        {
            if (!EnsureBroker(restartBroker))
            {
                return null;
            }

            HardwareMonitorBrokerResponse response = SendRequest(HardwareMonitorBrokerProtocol.SnapshotCommand);
            if (!string.IsNullOrWhiteSpace(response.Error))
            {
                AppLog.Write(response.Error);
                return null;
            }

            return new HardwareMonitorSnapshot(response.Sensors, response.CapturedAt, response.IsElevated);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return null;
        }
    }

    private bool EnsureBroker(bool forceRestart)
    {
        if (forceRestart)
        {
            DisposeBrokerProcess();
        }

        if (_brokerProcess is { HasExited: false })
        {
            return true;
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        string brokerPath = HardwareBrokerExecutableResolver.GetBrokerExecutablePath(processPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = brokerPath,
            Arguments = BuildBrokerArguments(_pipeName),
            WorkingDirectory = Path.GetDirectoryName(brokerPath) ?? AppContext.BaseDirectory,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        if (!CurrentProcessPrivilege.IsAdministrator())
        {
            startInfo.Verb = "runas";
        }

        _brokerProcess = Process.Start(startInfo);
        if (_brokerProcess is null)
        {
            return false;
        }

        BrokerProcessStarted?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static string BuildBrokerArguments(string pipeName)
    {
        return string.Join(
            " ",
            [
                QuoteArgument(HardwareMonitorBrokerProtocol.BrokerArgument),
                QuoteArgument(HardwareMonitorBrokerProtocol.PipeArgument),
                QuoteArgument(pipeName),
                QuoteArgument(HardwareMonitorBrokerProtocol.ParentArgument),
                QuoteArgument(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ]);
    }

    private HardwareMonitorBrokerResponse SendRequest(string command)
    {
        using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.None);
        client.Connect((int)ConnectTimeout.TotalMilliseconds);
        using var input = new StreamReader(client, leaveOpen: true);
        using var output = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
        output.WriteLine(JsonSerializer.Serialize(new HardwareMonitorBrokerRequest { Command = command }, JsonOptions));
        string? responseJson = input.ReadLine();
        return string.IsNullOrWhiteSpace(responseJson)
            ? new HardwareMonitorBrokerResponse { Error = "Hardware monitor broker returned an empty response." }
            : JsonSerializer.Deserialize<HardwareMonitorBrokerResponse>(responseJson, JsonOptions)
                ?? new HardwareMonitorBrokerResponse { Error = "Hardware monitor broker returned an invalid response." };
    }

    private void TrySendShutdown()
    {
        try
        {
            if (_brokerProcess is { HasExited: false })
            {
                _ = SendRequest(HardwareMonitorBrokerProtocol.ShutdownCommand);
            }
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private void DisposeBrokerProcess()
    {
        _brokerProcess?.Dispose();
        _brokerProcess = null;
    }

    private static HardwareMonitorSnapshot EmptySnapshot()
    {
        return new HardwareMonitorSnapshot([], DateTimeOffset.Now, CurrentProcessPrivilege.IsAdministrator());
    }

    private static string QuoteArgument(string argument)
    {
        return $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
