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

        bool started = TryStart(processPath, BuildDemotedArguments(arguments));
        return started ? UnelevatedRestartResult.Restarted : UnelevatedRestartResult.Failed;
    }

    public static string BuildDemotedArguments(IEnumerable<string> arguments)
    {
        string[] sourceArguments = arguments.ToArray();
        bool requestElevatedBroker = sourceArguments.Any(argument => string.Equals(argument, AdministratorRestartService.RestartArgument, StringComparison.OrdinalIgnoreCase))
            || sourceArguments.Any(argument => string.Equals(argument, LaunchOptions.ElevatedBrokerArgument, StringComparison.OrdinalIgnoreCase));
        return string.Join(
            " ",
            sourceArguments
                .Where(argument => !string.Equals(argument, AdministratorRestartService.RestartArgument, StringComparison.OrdinalIgnoreCase))
                .Where(argument => !string.Equals(argument, NoDemoteArgument, StringComparison.OrdinalIgnoreCase))
                .Where(argument => !string.Equals(argument, LaunchOptions.ElevatedBrokerArgument, StringComparison.OrdinalIgnoreCase))
                .Concat(requestElevatedBroker ? [LaunchOptions.ElevatedBrokerArgument] : [])
                .Append(NoDemoteArgument)
                .Select(QuoteArgument));
    }

    private static bool TryStart(string processPath, string arguments)
    {
        return TryStartWithLinkedToken(processPath, arguments) || TryStartWithShellToken(processPath, arguments);
    }

    private static bool TryStartWithLinkedToken(string processPath, string arguments)
    {
        IntPtr processToken = IntPtr.Zero;
        IntPtr linkedToken = IntPtr.Zero;
        IntPtr primaryToken = IntPtr.Zero;
        try
        {
            using Process currentProcess = Process.GetCurrentProcess();
            if (!NativeMethods.OpenProcessToken(currentProcess.Handle, NativeMethods.TOKEN_QUERY, out processToken))
            {
                return LogWin32Failure("OpenProcessToken(current)");
            }

            if (!NativeMethods.GetTokenInformation(
                processToken,
                NativeMethods.TokenLinkedToken,
                out NativeMethods.TOKEN_LINKED_TOKEN linkedTokenInfo,
                System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.TOKEN_LINKED_TOKEN>(),
                out _))
            {
                return LogWin32Failure("GetTokenInformation(TokenLinkedToken)");
            }

            linkedToken = linkedTokenInfo.LinkedToken;
            if (!NativeMethods.DuplicateTokenEx(linkedToken, PrimaryTokenAccess, IntPtr.Zero, NativeMethods.SecurityImpersonation, tokenType: 1, out primaryToken))
            {
                return LogWin32Failure("DuplicateTokenEx(linked)");
            }

            return TryCreateProcessWithToken(primaryToken, processPath, arguments, "CreateProcessWithTokenW(linked)");
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return false;
        }
        finally
        {
            CloseIfNeeded(primaryToken);
            CloseIfNeeded(linkedToken);
            CloseIfNeeded(processToken);
        }
    }

    private static bool TryStartWithShellToken(string processPath, string arguments)
    {
        IntPtr shellWindow = NativeMethods.GetShellWindow();
        if (shellWindow == IntPtr.Zero)
        {
            AppLog.Write("Cannot restart without elevation because the shell window is unavailable.");
            return false;
        }

        _ = NativeMethods.GetWindowThreadProcessId(shellWindow, out uint shellProcessId);
        if (shellProcessId == 0)
        {
            AppLog.Write("Cannot restart without elevation because the shell process id is unavailable.");
            return false;
        }

        IntPtr shellProcess = IntPtr.Zero;
        IntPtr shellToken = IntPtr.Zero;
        IntPtr primaryToken = IntPtr.Zero;
        try
        {
            shellProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, inheritHandle: false, shellProcessId);
            if (shellProcess == IntPtr.Zero)
            {
                return LogWin32Failure("OpenProcess(shell)");
            }

            if (!NativeMethods.OpenProcessToken(shellProcess, NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_QUERY, out shellToken))
            {
                return LogWin32Failure("OpenProcessToken(shell)");
            }

            if (!NativeMethods.DuplicateTokenEx(shellToken, PrimaryTokenAccess, IntPtr.Zero, NativeMethods.SecurityImpersonation, tokenType: 1, out primaryToken))
            {
                return LogWin32Failure("DuplicateTokenEx(shell)");
            }

            return TryCreateProcessWithToken(primaryToken, processPath, arguments, "CreateProcessWithTokenW(shell)");
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return false;
        }
        finally
        {
            CloseIfNeeded(primaryToken);
            CloseIfNeeded(shellToken);
            CloseIfNeeded(shellProcess);
        }
    }

    private static bool TryCreateProcessWithToken(IntPtr primaryToken, string processPath, string arguments, string operation)
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
            NativeMethods.CREATE_UNICODE_ENVIRONMENT,
            IntPtr.Zero,
            workingDirectory,
            ref startupInfo,
            out NativeMethods.PROCESS_INFORMATION processInformation))
        {
            return LogWin32Failure(operation);
        }

        NativeMethods.CloseHandle(processInformation.hThread);
        NativeMethods.CloseHandle(processInformation.hProcess);
        return true;
    }

    private static string QuoteArgument(string argument)
    {
        return $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static bool LogWin32Failure(string operation)
    {
        int error = Marshal.GetLastWin32Error();
        AppLog.Write($"{operation} failed with Win32 error {error}: {new Win32Exception(error).Message}");
        return false;
    }

    private static void CloseIfNeeded(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(handle);
        }
    }
}
