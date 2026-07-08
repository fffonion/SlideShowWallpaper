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
    private readonly Action _displayConfigurationChanged;
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
        Action<bool>? displayPowerPauseChanged = null,
        Action? displayConfigurationChanged = null)
    {
        _windowHandle = windowHandle;
        _profiles = profiles;
        _openSettings = openSettings;
        _exit = exit;
        _next = next;
        _togglePause = togglePause;
        _toggleStop = toggleStop;
        _minimizedChanged = minimizedChanged ?? (_ => { });
        _displayConfigurationChanged = displayConfigurationChanged ?? (() => { });
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

        if (msg == NativeMethods.WM_DISPLAYCHANGE)
        {
            _displayConfigurationChanged();
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
        SetCompactMenuImageStyle(menu);
        var menuBitmaps = new List<IntPtr>();

        IReadOnlyList<MonitorProfile> profiles = _profiles();
        foreach (TrayMenuItem item in BuildMenuItems(profiles))
        {
            AppendTrayMenuItem(menu, item, menuBitmaps);
        }

        foreach (TrayMenuItem item in BuildGlobalMenuItems(profiles))
        {
            AppendTrayMenuItem(menu, item, menuBitmaps);
        }

        NativeMethods.GetCursorPos(out NativeMethods.POINT point);
        NativeMethods.SetForegroundWindow(_windowHandle);
        uint command = NativeMethods.TrackPopupMenu(menu, NativeMethods.TPM_RIGHTBUTTON | NativeMethods.TPM_RETURNCMD, point.X, point.Y, 0, _windowHandle, IntPtr.Zero);
        NativeMethods.DestroyMenu(menu);
        DeleteMenuBitmaps(menuBitmaps);
        InvokeMenuCommand((int)command, profiles);
    }

    private static void SetCompactMenuImageStyle(IntPtr menu)
    {
        var info = new NativeMethods.MENUINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MENUINFO>(),
            fMask = NativeMethods.MIM_STYLE,
            dwStyle = NativeMethods.MNS_CHECKORBMP,
        };
        NativeMethods.SetMenuInfo(menu, ref info);
    }

    private static void AppendTrayMenuItem(IntPtr menu, TrayMenuItem item, List<IntPtr> menuBitmaps)
    {
        uint flags = item.Kind switch
        {
            TrayMenuItemKind.Header => NativeMethods.MF_STRING | NativeMethods.MF_GRAYED,
            TrayMenuItemKind.Separator => NativeMethods.MF_SEPARATOR,
            _ => item.IsEnabled ? NativeMethods.MF_STRING : (uint)(NativeMethods.MF_STRING | NativeMethods.MF_GRAYED),
        };
        NativeMethods.AppendMenu(menu, flags, (nuint)item.CommandId, item.Text);
        if (item.Kind == TrayMenuItemKind.Separator || item.Icon == TrayMenuIcon.None)
        {
            return;
        }

        IntPtr bitmap = TrayMenuBitmapFactory.Create(item.Icon, item.IsEnabled);
        if (bitmap == IntPtr.Zero)
        {
            return;
        }

        int itemPosition = NativeMethods.GetMenuItemCount(menu) - 1;
        if (itemPosition < 0)
        {
            NativeMethods.DeleteObject(bitmap);
            return;
        }

        var info = new NativeMethods.MENUITEMINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MENUITEMINFO>(),
            fMask = NativeMethods.MIIM_BITMAP,
            hbmpItem = bitmap,
        };
        if (NativeMethods.SetMenuItemInfo(menu, (uint)itemPosition, byPosition: true, ref info))
        {
            menuBitmaps.Add(bitmap);
        }
        else
        {
            NativeMethods.DeleteObject(bitmap);
        }
    }

    private static void DeleteMenuBitmaps(List<IntPtr> menuBitmaps)
    {
        foreach (IntPtr bitmap in menuBitmaps)
        {
            NativeMethods.DeleteObject(bitmap);
        }
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
}

internal static class TrayMenuBitmapFactory
{
    private const int Size = 16;
    private const int Foreground = unchecked((int)0xFF202020);
    private const int DisabledForeground = unchecked((int)0xFF909090);
    private const int Transparent = 0;

    public static IntPtr Create(TrayMenuIcon icon, bool isEnabled)
    {
        int[] pixels = Enumerable.Repeat(Transparent, Size * Size).ToArray();
        int color = isEnabled ? Foreground : DisabledForeground;
        DrawIcon(icon, pixels, color);

        var bitmapInfo = new NativeMethods.BITMAPINFO
        {
            bmiHeader = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = Size,
                biHeight = -Size,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
                biSizeImage = Size * Size * 4u,
            },
        };
        IntPtr bitmap = NativeMethods.CreateDIBSection(IntPtr.Zero, ref bitmapInfo, NativeMethods.DIB_RGB_COLORS, out IntPtr bits, IntPtr.Zero, 0);
        if (bitmap == IntPtr.Zero || bits == IntPtr.Zero)
        {
            return bitmap;
        }

        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bits, pixels.Length);
        return bitmap;
    }

    private static void DrawIcon(TrayMenuIcon icon, int[] pixels, int color)
    {
        switch (icon)
        {
            case TrayMenuIcon.Monitor:
                StrokeRect(pixels, 3, 3, 10, 7, color);
                FillRect(pixels, 7, 10, 2, 2, color);
                DrawLine(pixels, 5, 13, 10, 13, color);
                break;
            case TrayMenuIcon.Status:
                DrawLine(pixels, 4, 5, 12, 5, color);
                DrawLine(pixels, 4, 8, 10, 8, color);
                DrawLine(pixels, 4, 11, 12, 11, color);
                break;
            case TrayMenuIcon.NotLoaded:
                FillRect(pixels, 7, 3, 2, 7, color);
                FillRect(pixels, 7, 12, 2, 2, color);
                break;
            case TrayMenuIcon.Start:
                FillTriangleRight(pixels, 5, 3, 11, 8, 5, 13, color);
                break;
            case TrayMenuIcon.Stop:
                FillRect(pixels, 4, 4, 8, 8, color);
                break;
            case TrayMenuIcon.Pause:
                FillRect(pixels, 5, 4, 2, 8, color);
                FillRect(pixels, 9, 4, 2, 8, color);
                break;
            case TrayMenuIcon.Next:
                DrawLine(pixels, 3, 8, 11, 8, color);
                DrawLine(pixels, 8, 5, 11, 8, color);
                DrawLine(pixels, 8, 11, 11, 8, color);
                break;
            case TrayMenuIcon.Open:
                StrokeRect(pixels, 3, 4, 10, 8, color);
                DrawLine(pixels, 6, 3, 12, 3, color);
                break;
            case TrayMenuIcon.Exit:
                DrawLine(pixels, 4, 4, 12, 12, color);
                DrawLine(pixels, 12, 4, 4, 12, color);
                break;
        }
    }

    private static void FillRect(int[] pixels, int x, int y, int width, int height, int color)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
            {
                SetPixel(pixels, column, row, color);
            }
        }
    }

    private static void StrokeRect(int[] pixels, int x, int y, int width, int height, int color)
    {
        DrawLine(pixels, x, y, x + width, y, color);
        DrawLine(pixels, x, y + height, x + width, y + height, color);
        DrawLine(pixels, x, y, x, y + height, color);
        DrawLine(pixels, x + width, y, x + width, y + height, color);
    }

    private static void FillTriangleRight(int[] pixels, int leftX, int topY, int rightX, int middleY, int bottomX, int bottomY, int color)
    {
        for (int y = topY; y <= bottomY; y++)
        {
            double t = y <= middleY
                ? (double)(y - topY) / Math.Max(1, middleY - topY)
                : (double)(bottomY - y) / Math.Max(1, bottomY - middleY);
            int rowRight = leftX + (int)Math.Round((rightX - leftX) * t);
            for (int x = leftX; x <= rowRight; x++)
            {
                SetPixel(pixels, x, y, color);
            }
        }
    }

    private static void DrawLine(int[] pixels, int x0, int y0, int x1, int y1, int color)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            SetPixel(pixels, x0, y0, color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void SetPixel(int[] pixels, int x, int y, int color)
    {
        if (x < 0 || x >= Size || y < 0 || y >= Size)
        {
            return;
        }

        pixels[(y * Size) + x] = color;
    }
}
