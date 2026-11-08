using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class HardwareMonitorBrokerClient : IDisposable
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _syncRoot = new();
    private Process? _brokerProcess;
    private string _pipeName;
    private string[] _lastSnapshotSensorIds = [];
    private bool _hasSnapshotSensorIds;
    private bool _startElevated;
    private bool _usesExternalBroker;
    private bool _disposed;

    public HardwareMonitorBrokerClient()
        : this(HardwareMonitorBrokerProtocol.CreatePipeName())
    {
    }

    internal HardwareMonitorBrokerClient(string pipeName)
    {
        _pipeName = string.IsNullOrWhiteSpace(pipeName)
            ? HardwareMonitorBrokerProtocol.CreatePipeName()
            : pipeName;
    }

    public event EventHandler? BrokerProcessStarted;

    public static Process? StartBrokerProcess(string pipeName, int parentProcessId, bool requestElevation)
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        string brokerPath = HardwareBrokerExecutableResolver.GetBrokerExecutablePath(processPath);
        if (string.IsNullOrWhiteSpace(brokerPath))
        {
            AppLog.Write("Hardware monitor broker executable is unavailable.");
            return null;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = brokerPath,
            Arguments = BuildBrokerArguments(pipeName, parentProcessId),
            WorkingDirectory = Path.GetDirectoryName(brokerPath) ?? AppContext.BaseDirectory,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        if (requestElevation && !CurrentProcessPrivilege.IsElevated())
        {
            startInfo.Verb = "runas";
        }

        return Process.Start(startInfo);
    }

    public void UseBrokerPipe(string brokerPipeName)
    {
        if (string.IsNullOrWhiteSpace(brokerPipeName))
        {
            return;
        }

        lock (_syncRoot)
        {
            DisposeBrokerProcess();
            _pipeName = brokerPipeName;
            _usesExternalBroker = true;
            _lastSnapshotSensorIds = [];
            _hasSnapshotSensorIds = false;
        }
    }

    public void SetStartElevated(bool startElevated)
    {
        lock (_syncRoot)
        {
            _startElevated = startElevated;
        }
    }

    public bool StartBroker()
    {
        lock (_syncRoot)
        {
            return !_disposed && EnsureBroker(forceRestart: false);
        }
    }

    public void StopBroker()
    {
        lock (_syncRoot)
        {
            TrySendShutdown();
            DisposeBrokerProcess();
            _usesExternalBroker = false;
            _lastSnapshotSensorIds = [];
            _hasSnapshotSensorIds = false;
        }
    }

    public HardwareMonitorSnapshot GetSnapshot(HardwareMonitorConfig? config = null)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return EmptySnapshot();
            }

            IReadOnlyList<string> sensorIds = CreateRuntimeSensorIds(config);
            RestartBrokerWhenSensorFilterChanged(sensorIds);
            return TryGetSnapshot(sensorIds, restartBroker: false) ?? TryGetSnapshot(sensorIds, restartBroker: true) ?? EmptySnapshot();
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

            StopBroker();
            _disposed = true;
        }
    }

    private HardwareMonitorSnapshot? TryGetSnapshot(IReadOnlyList<string> sensorIds, bool restartBroker)
    {
        try
        {
            if (!EnsureBroker(restartBroker))
            {
                return null;
            }

            HardwareMonitorBrokerResponse response = SendRequest(HardwareMonitorBrokerProtocol.SnapshotCommand, sensorIds);
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
        if (_usesExternalBroker)
        {
            return true;
        }

        if (forceRestart)
        {
            DisposeBrokerProcess();
        }

        if (_brokerProcess is { HasExited: false })
        {
            return true;
        }

        _brokerProcess = StartBrokerProcess(
            _pipeName,
            Environment.ProcessId,
            _startElevated && !CurrentProcessPrivilege.IsElevated());
        if (_brokerProcess is null)
        {
            return false;
        }

        BrokerProcessStarted?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static string BuildBrokerArguments(string pipeName, int parentProcessId)
    {
        return string.Join(
            " ",
            [
                QuoteArgument(HardwareMonitorBrokerProtocol.BrokerArgument),
                QuoteArgument(HardwareMonitorBrokerProtocol.PipeArgument),
                QuoteArgument(pipeName),
                QuoteArgument(HardwareMonitorBrokerProtocol.ParentArgument),
                QuoteArgument(parentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ]);
    }

    private HardwareMonitorBrokerResponse SendRequest(string command, IReadOnlyList<string>? sensorIds = null)
    {
        // Hardware workers keep the synchronous client contract; transport waits are bounded and cancellable.
        return SendRequestAsync(_pipeName, command, sensorIds, RequestTimeout).GetAwaiter().GetResult();
    }

    internal static async Task<HardwareMonitorBrokerResponse> SendRequestAsync(
        string pipeName,
        string command,
        IReadOnlyList<string>? sensorIds,
        TimeSpan requestTimeout,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(requestTimeout);
        CancellationToken token = timeout.Token;
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(ConnectTimeout, token).ConfigureAwait(false);
        using var input = new StreamReader(client, leaveOpen: true);
        using var output = new StreamWriter(client, leaveOpen: true);
        string requestJson = JsonSerializer.Serialize(
            new HardwareMonitorBrokerRequest
            {
                Command = command,
                SensorIds = sensorIds?.ToList() ?? [],
            },
            JsonOptions);
        await output.WriteLineAsync(requestJson.AsMemory(), token).ConfigureAwait(false);
        await output.FlushAsync(token).ConfigureAwait(false);
        string? responseJson = await input.ReadLineAsync(token).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(responseJson)
            ? new HardwareMonitorBrokerResponse { Error = "Hardware monitor broker returned an empty response." }
            : JsonSerializer.Deserialize<HardwareMonitorBrokerResponse>(responseJson, JsonOptions)
                ?? new HardwareMonitorBrokerResponse { Error = "Hardware monitor broker returned an invalid response." };
    }

    private void TrySendShutdown()
    {
        try
        {
            if (_usesExternalBroker || _brokerProcess is { HasExited: false })
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
        return new HardwareMonitorSnapshot([], DateTimeOffset.Now, IsElevated: false);
    }

    private static IReadOnlyList<string> CreateRuntimeSensorIds(HardwareMonitorConfig? config)
    {
        if (config is null)
        {
            return [];
        }

        var sensorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string sensorId in config.SelectedSensorIds)
        {
            if (!string.IsNullOrWhiteSpace(sensorId))
            {
                sensorIds.Add(sensorId);
            }
        }

        foreach (HardwareOverlayElement element in config.Elements)
        {
            if (element.Kind == HardwareOverlayElementKind.Sensor && !string.IsNullOrWhiteSpace(element.SensorId))
            {
                sensorIds.Add(element.SensorId);
            }
        }

        return sensorIds
            .OrderBy(sensorId => sensorId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void RestartBrokerWhenSensorFilterChanged(IReadOnlyList<string> sensorIds)
    {
        string[] normalizedSensorIds = sensorIds.ToArray();
        if (!_usesExternalBroker
            && _hasSnapshotSensorIds
            && !normalizedSensorIds.SequenceEqual(_lastSnapshotSensorIds, StringComparer.OrdinalIgnoreCase))
        {
            TrySendShutdown();
            DisposeBrokerProcess();
        }

        _lastSnapshotSensorIds = normalizedSensorIds;
        _hasSnapshotSensorIds = true;
    }

    private static string QuoteArgument(string argument)
    {
        return $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
