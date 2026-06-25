using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SlideShowWallpaper.Interop;

namespace SlideShowWallpaper.Services;

public enum UnelevatedRestartResult
{
    NotElevated,
    Restarted,
    Failed,
}

public sealed class UnelevatedRestartService
{
    public const string NoDemoteArgument = "/no-demote";
    private const uint PrimaryTokenAccess = NativeMethods.TOKEN_ASSIGN_PRIMARY
        | NativeMethods.TOKEN_DUPLICATE
        | NativeMethods.TOKEN_QUERY
        | NativeMethods.TOKEN_ADJUST_DEFAULT
        | NativeMethods.TOKEN_ADJUST_SESSIONID;

    public UnelevatedRestartResult RestartIfCurrentProcessIsElevated(IEnumerable<string> arguments)
    {
        if (!CurrentProcessPrivilege.IsElevated())
        {
            return UnelevatedRestartResult.NotElevated;
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            AppLog.Write("Cannot restart without elevation because the current process path is unavailable.");
            return UnelevatedRestartResult.Failed;
        }

        string brokerPipeName = HardwareMonitorBrokerProtocol.CreatePipeName();
        PendingDemotedProcess? demotedProcess = TryStart(processPath, BuildDemotedArguments(arguments, brokerPipeName));
        if (demotedProcess is null)
        {
            return UnelevatedRestartResult.Failed;
        }

        using (demotedProcess)
        {
            Process? brokerProcess = HardwareMonitorBrokerClient.StartBrokerProcess(
                brokerPipeName,
                demotedProcess.ProcessId,
                requestElevation: false);
            if (brokerProcess is null)
            {
                demotedProcess.Terminate();
                AppLog.Write("Demoted main process was created, but the elevated hardware broker could not be started.");
                return UnelevatedRestartResult.Failed;
            }

            brokerProcess.Dispose();
            if (!demotedProcess.Resume())
            {
                return UnelevatedRestartResult.Failed;
            }
        }

        return UnelevatedRestartResult.Restarted;
    }

    public static string BuildDemotedArguments(IEnumerable<string> arguments, string? brokerPipeName = null)
    {
        List<string> demotedArguments = CreateDemotedArguments(arguments).ToList();
        if (!string.IsNullOrWhiteSpace(brokerPipeName))
        {
            demotedArguments.Add(LaunchOptions.HardwareBrokerPipeArgument);
            demotedArguments.Add(brokerPipeName);
        }

        demotedArguments.Add(NoDemoteArgument);
        return string.Join(" ", demotedArguments.Select(QuoteArgument));
    }

    private static PendingDemotedProcess? TryStart(string processPath, string arguments)
    {
        return TryStartWithLinkedToken(processPath, arguments) ?? TryStartWithShellToken(processPath, arguments);
    }

    private static PendingDemotedProcess? TryStartWithLinkedToken(string processPath, string arguments)
    {
        IntPtr processToken = IntPtr.Zero;
        IntPtr linkedToken = IntPtr.Zero;
        IntPtr primaryToken = IntPtr.Zero;
        try
        {
            using Process currentProcess = Process.GetCurrentProcess();
            if (!NativeMethods.OpenProcessToken(currentProcess.Handle, NativeMethods.TOKEN_QUERY, out processToken))
            {
                LogWin32Failure("OpenProcessToken(current)");
                return null;
            }

            if (!NativeMethods.GetTokenInformation(
                processToken,
                NativeMethods.TokenLinkedToken,
                out NativeMethods.TOKEN_LINKED_TOKEN linkedTokenInfo,
                System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.TOKEN_LINKED_TOKEN>(),
                out _))
            {
                LogWin32Failure("GetTokenInformation(TokenLinkedToken)");
                return null;
            }

            linkedToken = linkedTokenInfo.LinkedToken;
            if (!NativeMethods.DuplicateTokenEx(linkedToken, PrimaryTokenAccess, IntPtr.Zero, NativeMethods.SecurityImpersonation, tokenType: 1, out primaryToken))
            {
                LogWin32Failure("DuplicateTokenEx(linked)");
                return null;
            }

            return TryCreateProcessWithToken(primaryToken, processPath, arguments, "CreateProcessWithTokenW(linked)");
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return null;
        }
        finally
        {
            CloseIfNeeded(primaryToken);
            CloseIfNeeded(linkedToken);
            CloseIfNeeded(processToken);
        }
    }

    private static PendingDemotedProcess? TryStartWithShellToken(string processPath, string arguments)
    {
        IntPtr shellWindow = NativeMethods.GetShellWindow();
        if (shellWindow == IntPtr.Zero)
        {
            AppLog.Write("Cannot restart without elevation because the shell window is unavailable.");
            return null;
        }

        _ = NativeMethods.GetWindowThreadProcessId(shellWindow, out uint shellProcessId);
        if (shellProcessId == 0)
        {
            AppLog.Write("Cannot restart without elevation because the shell process id is unavailable.");
            return null;
        }

        IntPtr shellProcess = IntPtr.Zero;
        IntPtr shellToken = IntPtr.Zero;
        IntPtr primaryToken = IntPtr.Zero;
        try
        {
            shellProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, inheritHandle: false, shellProcessId);
            if (shellProcess == IntPtr.Zero)
            {
                LogWin32Failure("OpenProcess(shell)");
                return null;
            }

            if (!NativeMethods.OpenProcessToken(shellProcess, NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_QUERY, out shellToken))
            {
                LogWin32Failure("OpenProcessToken(shell)");
                return null;
            }

            if (!NativeMethods.DuplicateTokenEx(shellToken, PrimaryTokenAccess, IntPtr.Zero, NativeMethods.SecurityImpersonation, tokenType: 1, out primaryToken))
            {
                LogWin32Failure("DuplicateTokenEx(shell)");
                return null;
            }

            return TryCreateProcessWithToken(primaryToken, processPath, arguments, "CreateProcessWithTokenW(shell)");
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return null;
        }
        finally
        {
            CloseIfNeeded(primaryToken);
            CloseIfNeeded(shellToken);
            CloseIfNeeded(shellProcess);
        }
    }

    private static PendingDemotedProcess? TryCreateProcessWithToken(IntPtr primaryToken, string processPath, string arguments, string operation)
    {
        var startupInfo = new NativeMethods.STARTUPINFO
        {
            cb = Marshal.SizeOf<NativeMethods.STARTUPINFO>(),
        };
        string workingDirectory = Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
        var commandLine = new StringBuilder($"{QuoteArgument(processPath)} {arguments}".TrimEnd());
        if (!NativeMethods.CreateProcessWithTokenW(
            primaryToken,
            logonFlags: 0,
            processPath,
            commandLine,
            NativeMethods.CREATE_UNICODE_ENVIRONMENT | NativeMethods.CREATE_SUSPENDED,
            IntPtr.Zero,
            workingDirectory,
            ref startupInfo,
            out NativeMethods.PROCESS_INFORMATION processInformation))
        {
            LogWin32Failure(operation);
            return null;
        }

        return new PendingDemotedProcess(
            processInformation.dwProcessId,
            processInformation.hProcess,
            processInformation.hThread);
    }

    private static IEnumerable<string> CreateDemotedArguments(IEnumerable<string> arguments)
    {
        string[] sourceArguments = arguments.ToArray();
        for (int index = 0; index < sourceArguments.Length; index++)
        {
            string argument = sourceArguments[index];
            if (string.Equals(argument, AdministratorRestartService.RestartArgument, StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, NoDemoteArgument, StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, LaunchOptions.ElevatedBrokerArgument, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(argument, LaunchOptions.HardwareBrokerPipeArgument, StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            yield return argument;
        }
    }

    private static string QuoteArgument(string argument)
    {
        return $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static void LogWin32Failure(string operation)
    {
        int error = Marshal.GetLastWin32Error();
        AppLog.Write($"{operation} failed with Win32 error {error}: {new Win32Exception(error).Message}");
    }

    private static void CloseIfNeeded(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    private sealed class PendingDemotedProcess(int processId, IntPtr processHandle, IntPtr threadHandle) : IDisposable
    {
        private IntPtr _processHandle = processHandle;
        private IntPtr _threadHandle = threadHandle;

        public int ProcessId { get; } = processId;

        public bool Resume()
        {
            if (_threadHandle == IntPtr.Zero)
            {
                return false;
            }

            uint result = NativeMethods.ResumeThread(_threadHandle);
            if (result == uint.MaxValue)
            {
                LogWin32Failure("ResumeThread(demoted)");
                Terminate();
                return false;
            }

            return true;
        }

        public void Terminate()
        {
            if (_processHandle != IntPtr.Zero)
            {
                NativeMethods.TerminateProcess(_processHandle, 1);
            }
        }

        public void Dispose()
        {
            CloseIfNeeded(_threadHandle);
            CloseIfNeeded(_processHandle);
            _threadHandle = IntPtr.Zero;
            _processHandle = IntPtr.Zero;
        }
    }
}
