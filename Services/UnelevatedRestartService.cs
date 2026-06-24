using System.Diagnostics;
using System.Text;
using SlideShowWallpaper.Interop;

namespace SlideShowWallpaper.Services;

public sealed class UnelevatedRestartService
{
    public const string NoDemoteArgument = "/no-demote";

    public bool TryRestartIfCurrentProcessIsElevated(IEnumerable<string> arguments)
    {
        if (!CurrentProcessPrivilege.IsAdministrator())
        {
            return false;
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        return TryStart(processPath, BuildDemotedArguments(arguments));
    }

    public static string BuildDemotedArguments(IEnumerable<string> arguments)
    {
        return string.Join(
            " ",
            arguments
                .Where(argument => !string.Equals(argument, AdministratorRestartService.RestartArgument, StringComparison.OrdinalIgnoreCase))
                .Where(argument => !string.Equals(argument, NoDemoteArgument, StringComparison.OrdinalIgnoreCase))
                .Append(NoDemoteArgument)
                .Select(QuoteArgument));
    }

    private static bool TryStart(string processPath, string arguments)
    {
        IntPtr processToken = IntPtr.Zero;
        IntPtr linkedToken = IntPtr.Zero;
        IntPtr primaryToken = IntPtr.Zero;
        try
        {
            using Process currentProcess = Process.GetCurrentProcess();
            if (!NativeMethods.OpenProcessToken(currentProcess.Handle, NativeMethods.TOKEN_QUERY, out processToken))
            {
                return false;
            }

            if (!NativeMethods.GetTokenInformation(
                processToken,
                NativeMethods.TokenLinkedToken,
                out NativeMethods.TOKEN_LINKED_TOKEN linkedTokenInfo,
                System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.TOKEN_LINKED_TOKEN>(),
                out _))
            {
                return false;
            }

            linkedToken = linkedTokenInfo.LinkedToken;
            const uint primaryTokenAccess = NativeMethods.TOKEN_ASSIGN_PRIMARY
                | NativeMethods.TOKEN_DUPLICATE
                | NativeMethods.TOKEN_QUERY
                | NativeMethods.TOKEN_ADJUST_DEFAULT
                | NativeMethods.TOKEN_ADJUST_SESSIONID;
            if (!NativeMethods.DuplicateTokenEx(linkedToken, primaryTokenAccess, IntPtr.Zero, NativeMethods.SecurityImpersonation, tokenType: 1, out primaryToken))
            {
                return false;
            }

            var startupInfo = new NativeMethods.STARTUPINFO
            {
                cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.STARTUPINFO>(),
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
                return false;
            }

            NativeMethods.CloseHandle(processInformation.hThread);
            NativeMethods.CloseHandle(processInformation.hProcess);
            return true;
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

    private static string QuoteArgument(string argument)
    {
        return $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    private static void CloseIfNeeded(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(handle);
        }
    }
}
