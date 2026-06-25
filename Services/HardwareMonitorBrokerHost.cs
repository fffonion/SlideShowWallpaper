using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class HardwareMonitorBrokerHost
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsBrokerMode(IEnumerable<string> arguments)
    {
        return arguments.Any(argument => string.Equals(argument, HardwareMonitorBrokerProtocol.BrokerArgument, StringComparison.OrdinalIgnoreCase));
    }

    public static int Run(string[] arguments)
    {
        try
        {
            IReadOnlyList<string> args = arguments;
            if (!HardwareMonitorBrokerProtocol.TryGetOption(args, HardwareMonitorBrokerProtocol.PipeArgument, out string pipeName))
            {
                return 2;
            }

            using var reader = new HardwareMonitorReader();
            using CancellationTokenSource cancellation = CreateParentProcessCancellation(args);
            RunServer(pipeName, reader, cancellation.Token);
            return 0;
        }
        catch (Exception)
        {
            return 1;
        }
    }

    private static void RunServer(string pipeName, HardwareMonitorReader reader, CancellationToken cancellationToken)
    {
        using NamedPipeServerStream server = CreateServer(pipeName);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                server.WaitForConnectionAsync(cancellationToken).GetAwaiter().GetResult();
                bool shouldStop = HandleRequest(server, reader, cancellationToken);
                DisconnectServer(server);
                if (shouldStop)
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
                DisconnectServer(server);
            }
        }
    }

    private static NamedPipeServerStream CreateServer(string pipeName)
    {
        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    private static void DisconnectServer(NamedPipeServerStream server)
    {
        if (!server.IsConnected)
        {
            return;
        }

        try
        {
            server.Disconnect();
        }
        catch (InvalidOperationException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static bool HandleRequest(Stream stream, HardwareMonitorReader reader, CancellationToken cancellationToken)
    {
        using var input = new StreamReader(stream, leaveOpen: true);
        using var output = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        string? requestJson = input.ReadLine();
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return false;
        }

        HardwareMonitorBrokerRequest? request = JsonSerializer.Deserialize<HardwareMonitorBrokerRequest>(requestJson, JsonOptions);
        if (string.Equals(request?.Command, HardwareMonitorBrokerProtocol.ShutdownCommand, StringComparison.OrdinalIgnoreCase))
        {
            WriteResponse(output, new HardwareMonitorBrokerResponse { IsElevated = CurrentProcessPrivilege.IsElevated() });
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        HardwareMonitorSnapshot snapshot = reader.GetSnapshot(request?.SensorIds);
        WriteResponse(output, new HardwareMonitorBrokerResponse
        {
            Sensors = snapshot.Sensors.ToList(),
            CapturedAt = snapshot.CapturedAt,
            IsElevated = snapshot.IsElevated,
        });
        return false;
    }

    private static void WriteResponse(TextWriter output, HardwareMonitorBrokerResponse response)
    {
        output.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static CancellationTokenSource CreateParentProcessCancellation(IReadOnlyList<string> arguments)
    {
        var cancellation = new CancellationTokenSource();
        if (!HardwareMonitorBrokerProtocol.TryGetOption(arguments, HardwareMonitorBrokerProtocol.ParentArgument, out string parentValue)
            || !int.TryParse(parentValue, out int parentProcessId))
        {
            return cancellation;
        }

        try
        {
            Process parent = Process.GetProcessById(parentProcessId);
            _ = Task.Run(() =>
            {
                try
                {
                    parent.WaitForExit();
                    cancellation.Cancel();
                }
                catch (Exception)
                {
                    cancellation.Cancel();
                }
                finally
                {
                    parent.Dispose();
                }
            });
        }
        catch (Exception)
        {
            cancellation.Cancel();
        }

        return cancellation;
    }
}
