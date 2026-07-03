using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint IconId = 1;
    private const uint CallbackMessage = NativeMethods.WM_APP + 42;
    private const int OpenCommandId = 100;
    private const int GlobalPauseCommandId = 101;
    private const int ExitCommandId = 102;
    private const int GlobalStopCommandId = 103;
    private readonly IntPtr _windowHandle;
    private readonly Action _openSettings;
    private readonly Action _exit;
    private readonly Action<string> _next;
    private readonly Action<string> _togglePause;
    private readonly Action<string> _toggleStop;
    private readonly Action<bool> _minimizedChanged;
    private readonly Func<IReadOnlyList<MonitorProfile>> _profiles;
    private readonly NativeMethods.WindowProc _windowProc;
    private readonly DisplayPowerPauseService? _displayPowerPauseService;
    private IntPtr _previousWindowProc;
    private IntPtr _iconHandle;
    private bool _disposed;

    public TrayIconService(
        IntPtr windowHandle,
        Func<IReadOnlyList<MonitorProfile>> profiles,
        Action openSettings,
        Action exit,
        Action<string> next,
        Action<string> togglePause,
        Action<string> toggleStop,
        Action<bool>? minimizedChanged = null,
        Action<bool>? displayPowerPauseChanged = null)
    {
        _windowHandle = windowHandle;
        _profiles = profiles;
        _openSettings = openSettings;
        _exit = exit;
        _next = next;
        _togglePause = togglePause;
        _toggleStop = toggleStop;
        _minimizedChanged = minimizedChanged ?? (_ => { });
        _windowProc = WndProc;
        _previousWindowProc = NativeMethods.SetWindowLongPtr(_windowHandle, NativeMethods.GWL_WNDPROC, _windowProc);
        if (displayPowerPauseChanged is not null)
        {
            _displayPowerPauseService = new DisplayPowerPauseService(_windowHandle, displayPowerPauseChanged);
        }

        AddIcon();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        var data = CreateNotifyData();
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
        if (_iconHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_iconHandle);
            _iconHandle = IntPtr.Zero;
        }

        _displayPowerPauseService?.Dispose();
        _disposed = true;
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == CallbackMessage)
        {
            int mouseMessage = lParam.ToInt32();
            if (mouseMessage == NativeMethods.WM_LBUTTONUP)
            {
                _openSettings();
                return IntPtr.Zero;
            }

            if (mouseMessage == NativeMethods.WM_RBUTTONUP)
            {
                ShowContextMenu();
                return IntPtr.Zero;
            }
        }

        if (msg == NativeMethods.WM_SIZE)
        {
            int sizeCommand = wParam.ToInt32();
            if (sizeCommand == NativeMethods.SIZE_MINIMIZED)
            {
                _minimizedChanged(true);
            }
            else if (sizeCommand is NativeMethods.SIZE_RESTORED or NativeMethods.SIZE_MAXIMIZED)
            {
                _minimizedChanged(false);
            }
        }

        if (msg == NativeMethods.WM_POWERBROADCAST)
        {
            _displayPowerPauseService?.HandlePowerBroadcast(wParam, lParam);
        }

        if (msg == NativeMethods.WM_DESTROY)
        {
            Dispose();
        }

        return NativeMethods.CallWindowProc(_previousWindowProc, hwnd, msg, wParam, lParam);
    }

    private void AddIcon()
    {
        string iconPath = AppIconPaths.ResolveShellIconPath(Environment.ProcessPath, AppContext.BaseDirectory);
        IntPtr[] icons = [IntPtr.Zero];
        int[] ids = [0];
        NativeMethods.PrivateExtractIcons(iconPath, 0, 32, 32, icons, ids, 1, 0);
        _iconHandle = icons[0];
        if (_iconHandle == IntPtr.Zero && !string.Equals(iconPath, Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), StringComparison.OrdinalIgnoreCase))
        {
            NativeMethods.PrivateExtractIcons(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), 0, 32, 32, icons, ids, 1, 0);
            _iconHandle = icons[0];
        }

        var data = CreateNotifyData();
        data.uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_TIP | NativeMethods.NIF_ICON;
        data.hIcon = _iconHandle;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref data);
    }

    private NativeMethods.NOTIFYICONDATA CreateNotifyData()
    {
        return new NativeMethods.NOTIFYICONDATA
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _windowHandle,
            uID = IconId,
            uCallbackMessage = CallbackMessage,
            szTip = LocalizedStrings.Get("AppTitle"),
        };
    }

    private void ShowContextMenu()
    {
        IntPtr menu = NativeMethods.CreatePopupMenu();

        IReadOnlyList<MonitorProfile> profiles = _profiles();
        foreach (TrayMenuItem item in BuildMenuItems(profiles))
        {
            AppendTrayMenuItem(menu, item);
        }

        foreach (TrayMenuItem item in BuildGlobalMenuItems(profiles))
        {
            AppendTrayMenuItem(menu, item);
        }

        NativeMethods.GetCursorPos(out NativeMethods.POINT point);
        NativeMethods.SetForegroundWindow(_windowHandle);
        uint command = NativeMethods.TrackPopupMenu(menu, NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD, point.X, point.Y, 0, _windowHandle, IntPtr.Zero);
        NativeMethods.DestroyMenu(menu);
        InvokeMenuCommand((int)command, profiles);
    }

    private static void AppendTrayMenuItem(IntPtr menu, TrayMenuItem item)
    {
        uint flags = item.Kind switch
        {
            TrayMenuItemKind.Header => NativeMethods.MF_STRING | NativeMethods.MF_GRAYED,
            TrayMenuItemKind.Separator => NativeMethods.MF_SEPARATOR,
            _ => item.IsEnabled ? NativeMethods.MF_STRING : (uint)(NativeMethods.MF_STRING | NativeMethods.MF_GRAYED),
        };
        NativeMethods.AppendMenu(menu, flags, (nuint)item.CommandId, item.DisplayText);
    }

    private void InvokeMenuCommand(int command, IReadOnlyList<MonitorProfile> profiles)
    {
        if (command == OpenCommandId)
        {
            _openSettings();
        }
        else if (command == GlobalPauseCommandId)
        {
            TogglePauseForAll(profiles);
        }
        else if (command == GlobalStopCommandId)
        {
            ToggleStopForAll(profiles);
        }
        else if (command == ExitCommandId)
        {
            _exit();
        }
        else if (command >= 2000 && command < 3000)
        {
            int index = command - 2000;
            if (index < profiles.Count)
            {
                _toggleStop(profiles[index].Id);
            }
        }
        else if (command >= 3000 && command < 4000)
        {
            int index = command - 3000;
            if (index < profiles.Count)
            {
                _togglePause(profiles[index].Id);
            }
        }
        else if (command >= 4000)
        {
            int index = command - 4000;
            if (index < profiles.Count)
            {
                _next(profiles[index].Id);
            }
        }
    }

    private void TogglePauseForAll(IReadOnlyList<MonitorProfile> profiles)
    {
        MonitorProfile[] loadedProfiles = profiles.Where(profile => !string.IsNullOrWhiteSpace(profile.FolderPath)).ToArray();
        bool resumeAll = loadedProfiles.Length > 0 && loadedProfiles.All(profile => profile.IsPaused);
        foreach (MonitorProfile profile in loadedProfiles.Where(profile => profile.IsPaused == resumeAll))
        {
            _togglePause(profile.Id);
        }
    }

    private void ToggleStopForAll(IReadOnlyList<MonitorProfile> profiles)
    {
        MonitorProfile[] loadedProfiles = profiles.Where(profile => !string.IsNullOrWhiteSpace(profile.FolderPath)).ToArray();
        bool startAll = loadedProfiles.Length > 0 && loadedProfiles.All(profile => profile.IsStopped);
        foreach (MonitorProfile profile in loadedProfiles.Where(profile => profile.IsStopped == startAll))
        {
            _toggleStop(profile.Id);
        }
    }

    public static IReadOnlyList<TrayMenuItem> BuildMenuItems(IReadOnlyList<MonitorProfile> profiles, Func<string, string>? getString = null)
    {
        getString ??= LocalizedStrings.Get;
        var items = new List<TrayMenuItem>();
        for (int i = 0; i < profiles.Count; i++)
        {
            MonitorProfile profile = profiles[i];
            items.Add(TrayMenuItem.Header(profile.DisplayName, TrayMenuIcon.Monitor));
            if (string.IsNullOrWhiteSpace(profile.FolderPath))
            {
                items.Add(TrayMenuItem.DisabledCommand(getString("NotLoaded"), profile.Id, TrayMenuIcon.NotLoaded));
            }
            else
            {
                items.Add(TrayMenuItem.DisabledCommand(PlaybackStatusFormatter.FormatCurrentIndex(profile.CurrentMediaIndex, profile.TotalMediaCount, getString("CurrentIndexFormat")), profile.Id, TrayMenuIcon.Status));
                items.Add(TrayMenuItem.CreateCommand(2000 + i, profile.IsStopped ? getString("Start") : getString("Stop"), profile.Id, profile.IsStopped ? TrayMenuIcon.Start : TrayMenuIcon.Stop));
                items.Add(TrayMenuItem.CreateCommand(3000 + i, profile.IsPaused ? getString("Resume") : getString("Pause"), profile.Id, profile.IsPaused ? TrayMenuIcon.Start : TrayMenuIcon.Pause));
                items.Add(TrayMenuItem.CreateCommand(4000 + i, getString("Next"), profile.Id, TrayMenuIcon.Next));
            }

            items.Add(TrayMenuItem.Separator());
        }

        return items;
    }

    public static IReadOnlyList<TrayMenuItem> BuildGlobalMenuItems(IReadOnlyList<MonitorProfile> profiles, Func<string, string>? getString = null)
    {
        getString ??= LocalizedStrings.Get;
        MonitorProfile[] loadedProfiles = profiles.Where(profile => !string.IsNullOrWhiteSpace(profile.FolderPath)).ToArray();
        bool hasLoadedProfile = loadedProfiles.Length > 0;
        bool allPaused = hasLoadedProfile && loadedProfiles.All(profile => profile.IsPaused);
        bool allStopped = hasLoadedProfile && loadedProfiles.All(profile => profile.IsStopped);

        return
        [
            TrayMenuItem.CreateCommand(OpenCommandId, getString("Open"), string.Empty, TrayMenuIcon.Open),
            hasLoadedProfile
                ? TrayMenuItem.CreateCommand(GlobalPauseCommandId, allPaused ? getString("ResumeAll") : getString("PauseAll"), string.Empty, allPaused ? TrayMenuIcon.Start : TrayMenuIcon.Pause)
                : TrayMenuItem.DisabledCommand(getString("PauseAll"), string.Empty, TrayMenuIcon.Pause),
            hasLoadedProfile
                ? TrayMenuItem.CreateCommand(GlobalStopCommandId, allStopped ? getString("StartAll") : getString("StopAll"), string.Empty, allStopped ? TrayMenuIcon.Start : TrayMenuIcon.Stop)
                : TrayMenuItem.DisabledCommand(getString("StopAll"), string.Empty, TrayMenuIcon.Stop),
            TrayMenuItem.CreateCommand(ExitCommandId, getString("Exit"), string.Empty, TrayMenuIcon.Exit),
        ];
    }
}

public enum TrayMenuItemKind
{
    Header,
    Command,
    Separator
}

public enum TrayMenuIcon
{
    None,
    Monitor,
    Status,
    NotLoaded,
    Start,
    Stop,
    Pause,
    Next,
    Open,
    Exit
}

public sealed record TrayMenuItem(TrayMenuItemKind Kind, int CommandId, string Text, string MonitorId, bool IsEnabled, TrayMenuIcon Icon)
{
    public string DisplayText => Icon == TrayMenuIcon.None ? Text : $"{GetIconGlyph(Icon)}  {Text}";

    public static TrayMenuItem Header(string text, TrayMenuIcon icon = TrayMenuIcon.None)
    {
        return new TrayMenuItem(TrayMenuItemKind.Header, 0, text, string.Empty, false, icon);
    }

    public static TrayMenuItem CreateCommand(int command, string text, string monitorId, TrayMenuIcon icon = TrayMenuIcon.None)
    {
        return new TrayMenuItem(TrayMenuItemKind.Command, command, text, monitorId, true, icon);
    }

    public static TrayMenuItem DisabledCommand(string text, string monitorId, TrayMenuIcon icon = TrayMenuIcon.None)
    {
        return new TrayMenuItem(TrayMenuItemKind.Command, 0, text, monitorId, false, icon);
    }

    public static TrayMenuItem Separator()
    {
        return new TrayMenuItem(TrayMenuItemKind.Separator, 0, string.Empty, string.Empty, false, TrayMenuIcon.None);
    }

    private static string GetIconGlyph(TrayMenuIcon icon)
    {
        return icon switch
        {
            TrayMenuIcon.Monitor => "\u25A3",
            TrayMenuIcon.Status => "#",
            TrayMenuIcon.NotLoaded => "!",
            TrayMenuIcon.Start => "\u25B6",
            TrayMenuIcon.Stop => "\u25A0",
            TrayMenuIcon.Pause => "\u2161",
            TrayMenuIcon.Next => "\u2192",
            TrayMenuIcon.Open => "\u25A1",
            TrayMenuIcon.Exit => "\u00D7",
            _ => string.Empty,
        };
    }
}
