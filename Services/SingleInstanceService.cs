using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace SlideShowWallpaper.Services;

public sealed class SingleInstanceService : IDisposable
{
    private static readonly TimeSpan DefaultNotificationTimeout = TimeSpan.FromSeconds(3);
    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCancellation;
    private Task? _listenerTask;
    private bool _ownsMutex;

    public SingleInstanceService()
        : this(CreateDefaultInstanceKey())
    {
    }

    public SingleInstanceService(string instanceKey)
    {
        string safeKey = ToSafeName(instanceKey);
        _mutexName = $@"Local\SlideShowWallpaper.{safeKey}";
        _pipeName = $"SlideShowWallpaper.{safeKey}";
    }

    public bool TryAcquirePrimary()
    {
        if (_mutex is not null)
        {
            return _ownsMutex;
        }

        _mutex = new Mutex(initiallyOwned: false, _mutexName, out bool createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
        }

        return createdNew;
    }

    public void StartActivationListener(Action activationRequested)
    {
        if (!_ownsMutex || _listenerTask is not null)
        {
            return;
        }

        _listenerCancellation = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenAsync(activationRequested, _listenerCancellation.Token));
    }

    public async Task<bool> NotifyPrimaryAsync(TimeSpan? timeout = null)
    {
        TimeSpan effectiveTimeout = timeout ?? DefaultNotificationTimeout;
        try
        {
            int timeoutMilliseconds = Math.Max(1, (int)Math.Ceiling(effectiveTimeout.TotalMilliseconds));
            using var timeoutCancellation = new CancellationTokenSource(effectiveTimeout);
            await using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(timeoutMilliseconds, timeoutCancellation.Token).ConfigureAwait(false);
            byte[] message = [1];
            await client.WriteAsync(message, timeoutCancellation.Token).ConfigureAwait(false);
            await client.FlushAsync(timeoutCancellation.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or TimeoutException)
        {
            AppLog.Write(exception);
            return false;
        }
    }

    public void Dispose()
    {
        _listenerCancellation?.Cancel();
        _listenerCancellation?.Dispose();
        _listenerCancellation = null;

        _mutex?.Dispose();
        _mutex = null;
        _ownsMutex = false;
    }

    private async Task ListenAsync(Action activationRequested, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken);
                byte[] buffer = new byte[1];
                _ = await server.ReadAsync(buffer, cancellationToken);
                activationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                AppLog.Write(exception);
            }
        }
    }

    private static string CreateDefaultInstanceKey()
    {
        string userSid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        string installPath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        return $"{userSid}|{installPath}";
    }

    private static string ToSafeName(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }
}
