using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SlideShowWallpaper.Services;

public static class CurrentProcessPrivilege
{
    public static bool IsElevated()
    {
        IntPtr token = IntPtr.Zero;
        try
        {
            using Process currentProcess = Process.GetCurrentProcess();
            if (!ProcessPrivilegeNativeMethods.OpenProcessToken(currentProcess.Handle, ProcessPrivilegeNativeMethods.TOKEN_QUERY, out token))
            {
                LogWin32Failure("OpenProcessToken");
                return false;
            }

            if (!ProcessPrivilegeNativeMethods.GetTokenInformation(
                token,
                ProcessPrivilegeNativeMethods.TokenElevation,
                out ProcessPrivilegeNativeMethods.TOKEN_ELEVATION elevation,
                Marshal.SizeOf<ProcessPrivilegeNativeMethods.TOKEN_ELEVATION>(),
                out _))
            {
                LogWin32Failure("GetTokenInformation(TokenElevation)");
                return false;
            }

            return elevation.TokenIsElevated != 0;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return false;
        }
        finally
        {
            if (token != IntPtr.Zero)
            {
                ProcessPrivilegeNativeMethods.CloseHandle(token);
            }
        }
    }

    private static void LogWin32Failure(string operation)
    {
        int error = Marshal.GetLastWin32Error();
        AppLog.Write($"{operation} failed with Win32 error {error}: {new Win32Exception(error).Message}");
    }
}
