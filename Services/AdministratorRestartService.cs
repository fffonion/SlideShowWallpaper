using System.Diagnostics;

namespace SlideShowWallpaper.Services;

public sealed class AdministratorRestartService
{
    public const string RestartArgument = "/restart-elevated";

    public bool TryRestart()
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return false;
        }

        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = RestartArgument,
                WorkingDirectory = Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory,
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal,
            });
            return process is not null;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return false;
        }
    }
}
