using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;

namespace SlideShowWallpaper.Services;

public sealed class AutostartService
{
    private const string QuietArgument = "/q";
    private const string StartupFileName = "SlideShowWallpaper.lnk";
    private const string TaskName = "SlideShowWallpaper";
    private readonly Func<string> _processPathProvider;
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly IShortcutFileService _shortcutFileService;
    private readonly string _startupShortcutPath;

    public AutostartService()
        : this(GetDefaultStartupShortcutPath(), () => Environment.ProcessPath ?? string.Empty)
    {
    }

    public AutostartService(string startupShortcutPath, Func<string> processPathProvider)
        : this(startupShortcutPath, processPathProvider, new WindowsShortcutFileService())
    {
    }

    internal AutostartService(string startupShortcutPath, Func<string> processPathProvider, IShortcutFileService shortcutFileService)
        : this(startupShortcutPath, processPathProvider, shortcutFileService, new WindowsScheduledTaskService())
    {
    }

    internal AutostartService(
        string startupShortcutPath,
        Func<string> processPathProvider,
        IShortcutFileService shortcutFileService,
        IScheduledTaskService scheduledTaskService)
    {
        _startupShortcutPath = startupShortcutPath;
        _processPathProvider = processPathProvider;
        _shortcutFileService = shortcutFileService;
        _scheduledTaskService = scheduledTaskService;
    }

    public bool IsEnabled()
    {
        string processPath = _processPathProvider();
        return !string.IsNullOrWhiteSpace(processPath)
            && (_shortcutFileService.Matches(_startupShortcutPath, processPath, QuietArgument)
                || _scheduledTaskService.Matches(TaskName, processPath, QuietArgument));
    }

    public bool IsRunAsAdministratorEnabled()
    {
        string processPath = _processPathProvider();
        return !string.IsNullOrWhiteSpace(processPath)
            && _scheduledTaskService.Matches(TaskName, processPath, QuietArgument);
    }

    public void SetEnabled(bool enabled)
    {
        SetEnabled(enabled, runAsAdministrator: false);
    }

    public void SetEnabled(bool enabled, bool runAsAdministrator)
    {
        if (!enabled)
        {
            DeleteStartupFiles();
            return;
        }

        string processPath = _processPathProvider();
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return;
        }

        if (runAsAdministrator)
        {
            _scheduledTaskService.CreateLogonTask(
                TaskName,
                processPath,
                QuietArgument,
                Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory);
            TryDelete(_startupShortcutPath);
            DeleteLegacyCommandFile();
            return;
        }

        string? folder = Path.GetDirectoryName(_startupShortcutPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        _shortcutFileService.Create(
            _startupShortcutPath,
            processPath,
            QuietArgument,
            Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory);
        _scheduledTaskService.Delete(TaskName);
        DeleteLegacyCommandFile();
    }

    private void DeleteStartupFiles()
    {
        TryDelete(_startupShortcutPath);
        _scheduledTaskService.Delete(TaskName);
        DeleteLegacyCommandFile();
    }

    private void DeleteLegacyCommandFile()
    {
        string legacyPath = Path.ChangeExtension(_startupShortcutPath, ".cmd");
        if (!string.Equals(legacyPath, _startupShortcutPath, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(legacyPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private static string GetDefaultStartupShortcutPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", "Startup", StartupFileName);
    }
}

internal interface IScheduledTaskService
{
    void CreateLogonTask(string taskName, string targetPath, string arguments, string workingDirectory);

    void Delete(string taskName);

    bool Matches(string taskName, string targetPath, string arguments);
}

internal sealed class WindowsScheduledTaskService : IScheduledTaskService
{
    private const string TaskXmlNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public void CreateLogonTask(string taskName, string targetPath, string arguments, string workingDirectory)
    {
        string xmlPath = Path.Combine(Path.GetTempPath(), $"SlideShowWallpaper-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, CreateTaskXml(targetPath, arguments, workingDirectory), Encoding.Unicode);
            RunSchTasks(["/Create", "/TN", taskName, "/XML", xmlPath, "/F"], elevate: !CurrentProcessPrivilege.IsElevated());
        }
        finally
        {
            TryDelete(xmlPath);
        }
    }

    public void Delete(string taskName)
    {
        SchTasksResult result = RunSchTasks(["/Delete", "/TN", taskName, "/F"], elevate: false, ignoreFailure: true);
        if (result.ExitCode != 0 && !CurrentProcessPrivilege.IsElevated() && Exists(taskName))
        {
            RunSchTasks(["/Delete", "/TN", taskName, "/F"], elevate: true, ignoreFailure: true);
        }
    }

    public bool Matches(string taskName, string targetPath, string arguments)
    {
        SchTasksResult result = RunSchTasks(["/Query", "/TN", taskName, "/XML"], elevate: false, ignoreFailure: true);
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.Output))
        {
            return false;
        }

        try
        {
            XDocument document = XDocument.Parse(result.Output);
            XNamespace ns = TaskXmlNamespace;
            string command = document.Descendants(ns + "Command").FirstOrDefault()?.Value ?? string.Empty;
            string taskArguments = document.Descendants(ns + "Arguments").FirstOrDefault()?.Value ?? string.Empty;
            string runLevel = document.Descendants(ns + "RunLevel").FirstOrDefault()?.Value ?? string.Empty;
            return string.Equals(command, targetPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(taskArguments, arguments, StringComparison.OrdinalIgnoreCase)
                && string.Equals(runLevel, "HighestAvailable", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return false;
        }
    }

    private static string CreateTaskXml(string targetPath, string arguments, string workingDirectory)
    {
        string userName = WindowsIdentity.GetCurrent().Name;
        return $$"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.4" xmlns="{{TaskXmlNamespace}}">
              <RegistrationInfo>
                <Description>SlideShow Wallpaper quiet startup</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{{SecurityElement.Escape(userName)}}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{{SecurityElement.Escape(userName)}}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{{SecurityElement.Escape(targetPath)}}</Command>
                  <Arguments>{{SecurityElement.Escape(arguments)}}</Arguments>
                  <WorkingDirectory>{{SecurityElement.Escape(workingDirectory)}}</WorkingDirectory>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static bool Exists(string taskName)
    {
        SchTasksResult result = RunSchTasks(["/Query", "/TN", taskName], elevate: false, ignoreFailure: true);
        return result.ExitCode == 0;
    }

    private static SchTasksResult RunSchTasks(
        IReadOnlyList<string> arguments,
        bool elevate,
        bool ignoreFailure = false)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "schtasks.exe",
            UseShellExecute = elevate,
            CreateNoWindow = !elevate,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            RedirectStandardOutput = !elevate,
            RedirectStandardError = !elevate,
        };
        if (elevate)
        {
            startInfo.Verb = "runas";
            startInfo.Arguments = string.Join(' ', arguments.Select(QuoteArgument));
        }
        else
        {
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start schtasks.exe.");
            string output = elevate ? string.Empty : process.StandardOutput.ReadToEnd();
            string error = elevate ? string.Empty : process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 && !ignoreFailure)
            {
                throw new InvalidOperationException($"schtasks.exe failed with exit code {process.ExitCode}. {error}");
            }

            return new SchTasksResult(process.ExitCode, output, error);
        }
        catch (Exception exception) when (ignoreFailure)
        {
            AppLog.Write(exception);
            return new SchTasksResult(-1, string.Empty, exception.Message);
        }
    }

    private static string QuoteArgument(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal)
            ? "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : value;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }
    }

    private sealed record SchTasksResult(int ExitCode, string Output, string Error);
}

internal interface IShortcutFileService
{
    void Create(string shortcutPath, string targetPath, string arguments, string workingDirectory);

    bool Matches(string shortcutPath, string targetPath, string arguments);
}

internal sealed class WindowsShortcutFileService : IShortcutFileService
{
    private static readonly Guid ShellLinkClassId = new("00021401-0000-0000-C000-000000000046");
    private const int MaxPath = 260;

    public void Create(string shortcutPath, string targetPath, string arguments, string workingDirectory)
    {
        IShellLinkW shellLink = CreateShellLink();
        shellLink.SetPath(targetPath);
        shellLink.SetArguments(arguments);
        shellLink.SetWorkingDirectory(workingDirectory);
        shellLink.SetIconLocation(targetPath, 0);
        ((IPersistFile)shellLink).Save(shortcutPath, true);
    }

    public bool Matches(string shortcutPath, string targetPath, string arguments)
    {
        if (!File.Exists(shortcutPath))
        {
            return false;
        }

        IShellLinkW shellLink = CreateShellLink();
        ((IPersistFile)shellLink).Load(shortcutPath, 0);
        var targetBuilder = new StringBuilder(MaxPath);
        var argumentsBuilder = new StringBuilder(MaxPath);
        shellLink.GetPath(targetBuilder, targetBuilder.Capacity, IntPtr.Zero, 0);
        shellLink.GetArguments(argumentsBuilder, argumentsBuilder.Capacity);
        return string.Equals(targetBuilder.ToString(), targetPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(argumentsBuilder.ToString(), arguments, StringComparison.OrdinalIgnoreCase);
    }

    private static IShellLinkW CreateShellLink()
    {
        Type shellLinkType = Type.GetTypeFromCLSID(ShellLinkClassId)
            ?? throw new InvalidOperationException("ShellLink COM class is not available.");
        object shellLink = Activator.CreateInstance(shellLinkType)
            ?? throw new InvalidOperationException("Unable to create ShellLink.");
        return (IShellLinkW)shellLink;
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
            int cchMaxPath,
            IntPtr pfd,
            uint fFlags);

        void GetIDList(out IntPtr ppidl);

        void SetIDList(IntPtr pidl);

        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);

        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);

        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);

        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

        void GetHotkey(out short pwHotkey);

        void SetHotkey(short wHotkey);

        void GetShowCmd(out int piShowCmd);

        void SetShowCmd(int iShowCmd);

        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);

        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);

        void Resolve(IntPtr hwnd, uint fFlags);

        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
