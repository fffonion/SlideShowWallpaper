using System.IO.Pipes;
using System.Text.Json;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class HardwareMonitorBrokerClientTests
{
    [Fact]
    public async Task SendRequestAsync_ConnectedBrokerDoesNotReply_CancelsAtDeadline()
    {
        string pipeName = HardwareMonitorBrokerProtocol.CreatePipeName();
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        Task<HardwareMonitorBrokerResponse> request = HardwareMonitorBrokerClient.SendRequestAsync(pipeName, "snapshot", [], TimeSpan.FromSeconds(1));
        await server.WaitForConnectionAsync(safety.Token);
        using var input = new StreamReader(server, leaveOpen: true);
        Assert.NotNull(await input.ReadLineAsync(safety.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(safety.Token));
    }

    [Fact]
    public async Task SendRequestAsync_CallerCancelsAfterConnection_StopsWaitingForReply()
    {
        string pipeName = HardwareMonitorBrokerProtocol.CreatePipeName();
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var cancellation = new CancellationTokenSource();
        Task<HardwareMonitorBrokerResponse> request = HardwareMonitorBrokerClient.SendRequestAsync(pipeName, "snapshot", [], TimeSpan.FromSeconds(30), cancellation.Token);
        await server.WaitForConnectionAsync(safety.Token);
        using var input = new StreamReader(server, leaveOpen: true);
        Assert.NotNull(await input.ReadLineAsync(safety.Token));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(safety.Token));
    }

    [Fact]
    public async Task SendRequestAsync_MissingBroker_RespectsRequestDeadline()
    {
        using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        Task<HardwareMonitorBrokerResponse> request = HardwareMonitorBrokerClient.SendRequestAsync(HardwareMonitorBrokerProtocol.CreatePipeName(), "snapshot", [], TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(safety.Token));
    }

    [Fact]
    public async Task SendRequestAsync_ValidReply_PreservesSensorFilterAndElevation()
    {
        string pipeName = HardwareMonitorBrokerProtocol.CreatePipeName();
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        Task<HardwareMonitorBrokerResponse> request = HardwareMonitorBrokerClient.SendRequestAsync(pipeName, "snapshot", ["cpu-temp"], TimeSpan.FromSeconds(5));
        await server.WaitForConnectionAsync(safety.Token);
        using var input = new StreamReader(server, leaveOpen: true);
        string json = (await input.ReadLineAsync(safety.Token))!;
        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("cpu-temp", document.RootElement.GetProperty("sensorIds")[0].GetString());
        using var output = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
        await output.WriteLineAsync(JsonSerializer.Serialize(new HardwareMonitorBrokerResponse { IsElevated = true }));

        HardwareMonitorBrokerResponse response = await request;

        Assert.True(response.IsElevated);
        Assert.Empty(response.Error);
    }

    [Fact]
    public async Task CachedSnapshot_WhileBrokerIsBusy_DoesNotWaitForPipeLock()
    {
        string pipeName = HardwareMonitorBrokerProtocol.CreatePipeName();
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var safety = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var requestReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReply = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new HardwareMonitorBrokerClient(pipeName);
        client.UseBrokerPipe(pipeName);
        using var service = new HardwareMonitorService(client);
        Task serve = Task.Run(async () =>
        {
            for (int index = 0; index < 2; index++)
            {
                await server.WaitForConnectionAsync(safety.Token);
                {
                    using var input = new StreamReader(server, leaveOpen: true);
                    Assert.NotNull(await input.ReadLineAsync(safety.Token));
                    if (index == 0)
                    {
                        requestReceived.SetResult();
                        await releaseReply.Task.WaitAsync(safety.Token);
                    }

                    using var output = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
                    await output.WriteLineAsync(JsonSerializer.Serialize(new HardwareMonitorBrokerResponse
                    {
                        IsElevated = true,
                        Sensors = [new HardwareSensorReading("cpu-temp", "CPU", "Package", HardwareMetricKind.Temperature, HardwareMetricGroup.Cpu, 55, "C")],
                    }));
                }

                server.Disconnect();
            }
        });
        Task<HardwareMonitorSnapshot> pending = Task.Run(() => service.GetSnapshot());
        await requestReceived.Task.WaitAsync(safety.Token);

        Assert.Null(await Task.Run(() => service.CachedSnapshot).WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.False(pending.IsCompleted);
        releaseReply.SetResult();
        HardwareMonitorSnapshot snapshot = await pending.WaitAsync(safety.Token);
        Assert.Same(snapshot, service.CachedSnapshot);
        Assert.True(service.CachedSnapshot!.IsElevated);

        await Task.Run(service.Dispose).WaitAsync(safety.Token);
        await serve.WaitAsync(safety.Token);
    }
}
