using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class MonitorService
{
    public IReadOnlyList<MonitorProfile> GetCurrentMonitors()
    {
        var monitors = new List<MonitorProfile>();
        int index = 1;

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref NativeMethods.RECT rect, IntPtr _) =>
        {
            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
            };

            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                string friendlyName = GetFriendlyName(info.szDevice);
                monitors.Add(new MonitorProfile
                {
                    Id = string.IsNullOrWhiteSpace(info.szDevice) ? $"Monitor{index}" : info.szDevice,
                    DisplayName = BuildDisplayName(info.szDevice, friendlyName),
                    OffsetX = 0,
                    OffsetY = 0,
                });
            }

            index++;
            return true;
        }, IntPtr.Zero);

        return monitors;
    }

    public IReadOnlyDictionary<string, NativeMethods.RECT> GetMonitorRects()
    {
        var rects = new Dictionary<string, NativeMethods.RECT>(StringComparer.OrdinalIgnoreCase);

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref NativeMethods.RECT rect, IntPtr _) =>
        {
            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
            };

            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                rects[info.szDevice] = TryGetDisplayModeRect(info.szDevice, out NativeMethods.RECT modeRect)
                    ? ToDesktopRect(modeRect)
                    : ToDesktopRect(info.rcMonitor);
            }

            return true;
        }, IntPtr.Zero);

        return rects;
    }

    public static NativeMethods.RECT ToDesktopRect(NativeMethods.RECT monitorRect) => monitorRect;

    public static string BuildDisplayName(string deviceName, string friendlyName)
    {
        string cleanDeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Display" : deviceName.Trim();
        string cleanFriendlyName = friendlyName.Trim();
        if (!string.IsNullOrWhiteSpace(cleanFriendlyName) && !string.Equals(cleanFriendlyName, cleanDeviceName, StringComparison.OrdinalIgnoreCase))
        {
            return cleanFriendlyName;
        }

        const string displayPrefix = @"\\.\DISPLAY";
        return cleanDeviceName.StartsWith(displayPrefix, StringComparison.OrdinalIgnoreCase)
            ? $"Display {cleanDeviceName[displayPrefix.Length..]}"
            : cleanDeviceName;
    }

    private static string GetFriendlyName(string deviceName)
    {
        var displayDevice = new NativeMethods.DISPLAY_DEVICE
        {
            cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>(),
        };

        if (NativeMethods.EnumDisplayDevices(deviceName, 0, ref displayDevice, 0)
            && !string.IsNullOrWhiteSpace(displayDevice.DeviceString))
        {
            return displayDevice.DeviceString;
        }

        displayDevice = new NativeMethods.DISPLAY_DEVICE
        {
            cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>(),
        };

        if (NativeMethods.EnumDisplayDevices(null, 0, ref displayDevice, 0)
            && !string.IsNullOrWhiteSpace(displayDevice.DeviceString))
        {
            return displayDevice.DeviceString;
        }

        return string.Empty;
    }

    private static bool TryGetDisplayModeRect(string deviceName, out NativeMethods.RECT rect)
    {
        var mode = new NativeMethods.DEVMODE
        {
            dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DEVMODE>(),
        };

        if (NativeMethods.EnumDisplaySettingsEx(deviceName, -1, ref mode, 0)
            && mode.dmPelsWidth > 0
            && mode.dmPelsHeight > 0)
        {
            rect = new NativeMethods.RECT
            {
                Left = mode.dmPositionX,
                Top = mode.dmPositionY,
                Right = mode.dmPositionX + mode.dmPelsWidth,
                Bottom = mode.dmPositionY + mode.dmPelsHeight,
            };
            return true;
        }

        rect = default;
        return false;
    }
}
