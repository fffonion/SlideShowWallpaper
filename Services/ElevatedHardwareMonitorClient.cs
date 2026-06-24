using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class ElevatedHardwareMonitorClient : IDisposable
{
    private const int ConnectionTimeoutMs = 20000;
    private const string SnapshotCommand = "snapshot";
    private const string ShutdownCommand = "shutdown";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _processPath;
    private readonly string _pipeName;
    private NamedPipeServerStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Process? _process;

    public ElevatedHardwareMonitorClient()
        : this(Environment.ProcessPath ?? string.Empty, $"SlideShowWallpaper.HardwareMonitor.{Environment.ProcessId}.{Guid.NewGuid():N}")
    {
    }

    internal ElevatedHardwareMonitorClient(string processPath, string pipeName)
    {
        _processPath = processPath;
        _pipeName = pipeName;
    }

    public static bool IsHelperMode(IEnumerable<string> arguments, out string pipeName)
    {
        string[] normalized = arguments.ToArray();
        int index = Array.FindIndex(normalized, argument => string.Equals(argument, LaunchOptions.HardwareMonitorHelperArgument, StringComparison.OrdinalIgnoreCase));
        if (index >= 0 && index + 1 < normalized.Length && !string.IsNullOrWhiteSpace(normalized[index + 1]))
        {
            pipeName = normalized[index + 1];
            return true;
        }

        pipeName = string.Empty;
        return false;
    }

    public bool Start()
    {
        if (_writer is not null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_processPath))
        {
            return false;
        }

        _pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        try
        {
            _process = Process.Start(new ProcessStartInfo
            {
                FileName = _processPath,
                Arguments = $"{LaunchOptions.HardwareMonitorHelperArgument} {QuoteArgument(_pipeName)}",
                WorkingDirectory = Path.GetDirectoryName(_processPath) ?? AppContext.BaseDirectory,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (_process is null)
            {
                DisposePipe();
                return false;
            }

            _pipe.WaitForConnectionAsync().Wait(TimeSpan.FromMilliseconds(ConnectionTimeoutMs));
            if (!_pipe.IsConnected)
            {
                DisposePipe();
                return false;
            }

            _reader = new StreamReader(_pipe);
            _writer = new StreamWriter(_pipe)
            {
                AutoFlush = true,
            };
            return true;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            DisposePipe();
            return false;
        }
    }

    public HardwareMonitorSnapshot? GetSnapshot()
    {
        if (_reader is null || _writer is null)
        {
            return null;
        }

        try
        {
            _writer.WriteLine(SnapshotCommand);
            string? json = _reader.ReadLine();
            HardwareMonitorSnapshot? snapshot = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<HardwareMonitorSnapshot>(json, SerializerOptions);
            return snapshot is null
                ? null
                : snapshot with { IsElevated = true };
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            DisposePipe();
            return null;
        }
    }

    public void Dispose()
    {
        try
        {
            _writer?.WriteLine(ShutdownCommand);
        }
        catch (IOException)
        {
        }

        DisposePipe();
        _process?.Dispose();
        _process = null;
    }

    public static void RunHelper(string pipeName)
    {
        using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
        pipe.Connect(ConnectionTimeoutMs);
        using var reader = new StreamReader(pipe);
        using var writer = new StreamWriter(pipe)
        {
            AutoFlush = true,
        };
        using var hardwareMonitorService = new HardwareMonitorService(useElevatedHelper: false);
        while (reader.ReadLine() is string command)
        {
            if (string.Equals(command, ShutdownCommand, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.Equals(command, SnapshotCommand, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            HardwareMonitorSnapshot snapshot = hardwareMonitorService.GetSnapshot() with { IsElevated = true };
            writer.WriteLine(JsonSerializer.Serialize(snapshot, SerializerOptions));
        }
    }

    private static string QuoteArgument(string argument)
    {
        return $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private void DisposePipe()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _pipe?.Dispose();
        _writer = null;
        _reader = null;
        _pipe = null;
    }
}
