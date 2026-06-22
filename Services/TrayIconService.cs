using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint IconId = 1;
    private const uint CallbackMessage = NativeMethods.WM_APP + 42;
    private readonly IntPtr _windowHandle;
    private readonly Action _openSettings;
    private readonly Action _exit;
    private readonly Action<string> _next;
    private readonly Action<string> _togglePause;
    private readonly Action<string> _toggleStop;
    private readonly Action<bool> _minimizedChanged;
    private readonly Func<IReadOnlyList<MonitorProfile>> _profiles;
    private readonly NativeMethods.WindowProc _windowProc;
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
        Action<bool>? minimizedChanged = null)
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
            uint flags = item.Kind switch
            {
                TrayMenuItemKind.Header => NativeMethods.MF_STRING | NativeMethods.MF_GRAYED,
                TrayMenuItemKind.Separator => NativeMethods.MF_SEPARATOR,
                _ => item.IsEnabled ? NativeMethods.MF_STRING : (uint)(NativeMethods.MF_STRING | NativeMethods.MF_GRAYED),
            };
            NativeMethods.AppendMenu(menu, flags, (nuint)item.CommandId, item.Text);
        }

        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, 100, LocalizedStrings.Get("Open"));
        NativeMethods.AppendMenu(menu, NativeMethods.MF_STRING, 102, LocalizedStrings.Get("Exit"));

        NativeMethods.GetCursorPos(out NativeMethods.POINT point);
        NativeMethods.SetForegroundWindow(_windowHandle);
        uint command = NativeMethods.TrackPopupMenu(menu, NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD, point.X, point.Y, 0, _windowHandle, IntPtr.Zero);
        NativeMethods.DestroyMenu(menu);
        InvokeMenuCommand((int)command, profiles);
    }

    private void InvokeMenuCommand(int command, IReadOnlyList<MonitorProfile> profiles)
    {
        if (command == 100)
        {
            _openSettings();
        }
        else if (command == 102)
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

    public static IReadOnlyList<TrayMenuItem> BuildMenuItems(IReadOnlyList<MonitorProfile> profiles, Func<string, string>? getString = null)
    {
        getString ??= LocalizedStrings.Get;
        var items = new List<TrayMenuItem>();
        for (int i = 0; i < profiles.Count; i++)
        {
            MonitorProfile profile = profiles[i];
            items.Add(TrayMenuItem.Header(profile.DisplayName));
            if (string.IsNullOrWhiteSpace(profile.FolderPath))
            {
                items.Add(TrayMenuItem.DisabledCommand(getString("NotLoaded"), profile.Id));
            }
            else
            {
                items.Add(TrayMenuItem.DisabledCommand(PlaybackStatusFormatter.FormatCurrentIndex(profile.CurrentMediaIndex, profile.TotalMediaCount, getString("CurrentIndexFormat")), profile.Id));
                items.Add(TrayMenuItem.CreateCommand(2000 + i, profile.IsStopped ? getString("Start") : getString("Stop"), profile.Id));
                items.Add(TrayMenuItem.CreateCommand(3000 + i, profile.IsPaused ? getString("Resume") : getString("Pause"), profile.Id));
                items.Add(TrayMenuItem.CreateCommand(4000 + i, getString("Next"), profile.Id));
            }

            items.Add(TrayMenuItem.Separator());
        }

        return items;
    }
}

public enum TrayMenuItemKind
{
    Header,
    Command,
    Separator
}

public sealed record TrayMenuItem(TrayMenuItemKind Kind, int CommandId, string Text, string MonitorId, bool IsEnabled)
{
    public static TrayMenuItem Header(string text)
    {
        return new TrayMenuItem(TrayMenuItemKind.Header, 0, text, string.Empty, false);
    }

    public static TrayMenuItem CreateCommand(int command, string text, string monitorId)
    {
        return new TrayMenuItem(TrayMenuItemKind.Command, command, text, monitorId, true);
    }

    public static TrayMenuItem DisabledCommand(string text, string monitorId)
    {
        return new TrayMenuItem(TrayMenuItemKind.Command, 0, text, monitorId, false);
    }

    public static TrayMenuItem Separator()
    {
        return new TrayMenuItem(TrayMenuItemKind.Separator, 0, string.Empty, string.Empty, false);
    }
}
