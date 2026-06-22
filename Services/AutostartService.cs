using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace SlideShowWallpaper.Services;

public sealed class AutostartService
{
    private const string QuietArgument = "/q";
    private const string StartupFileName = "SlideShowWallpaper.lnk";
    private readonly Func<string> _processPathProvider;
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
    {
        _startupShortcutPath = startupShortcutPath;
        _processPathProvider = processPathProvider;
        _shortcutFileService = shortcutFileService;
    }

    public bool IsEnabled()
    {
        string processPath = _processPathProvider();
        return !string.IsNullOrWhiteSpace(processPath)
            && _shortcutFileService.Matches(_startupShortcutPath, processPath, QuietArgument);
    }

    public void SetEnabled(bool enabled)
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
        DeleteLegacyCommandFile();
    }

    private void DeleteStartupFiles()
    {
        File.Delete(_startupShortcutPath);
        DeleteLegacyCommandFile();
    }

    private void DeleteLegacyCommandFile()
    {
        string legacyPath = Path.ChangeExtension(_startupShortcutPath, ".cmd");
        if (!string.Equals(legacyPath, _startupShortcutPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(legacyPath);
        }
    }

    private static string GetDefaultStartupShortcutPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", "Startup", StartupFileName);
    }
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
