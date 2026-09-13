using SlideShowWallpaper.Interop;
using SlideShowWallpaper.Models;
using System.Globalization;

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
                DisplayIdentity identity = GetDisplayIdentity(info.szDevice);
                monitors.Add(new MonitorProfile
                {
                    Id = ResolvePersistentId(info.szDevice, identity.DeviceId, identity.DeviceKey, index),
                    DeviceName = info.szDevice,
                    DisplayName = BuildDisplayName(info.szDevice, identity.FriendlyName),
                    OffsetX = 0,
                    OffsetY = 0,
                });
            }

            index++;
            return true;
        }, IntPtr.Zero);

        return SortBySystemDisplayNumber(monitors);
    }

    public IReadOnlyDictionary<string, NativeMethods.RECT> GetMonitorRects()
    {
        var rects = new Dictionary<string, NativeMethods.RECT>(StringComparer.OrdinalIgnoreCase);
        int index = 1;

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref NativeMethods.RECT rect, IntPtr _) =>
        {
            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
            };

            if (NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                DisplayIdentity identity = GetDisplayIdentity(info.szDevice);
                string monitorId = ResolvePersistentId(info.szDevice, identity.DeviceId, identity.DeviceKey, index);
                rects[monitorId] = TryGetDisplayModeRect(info.szDevice, out NativeMethods.RECT modeRect)
                    ? ToDesktopRect(modeRect)
                    : ToDesktopRect(info.rcMonitor);
            }

            index++;
            return true;
        }, IntPtr.Zero);

        return rects;
    }

    public static NativeMethods.RECT ToDesktopRect(NativeMethods.RECT monitorRect) => monitorRect;

    public static IReadOnlyList<MonitorProfile> SortBySystemDisplayNumber(IEnumerable<MonitorProfile> monitors)
    {
        return monitors
            .OrderBy(monitor => GetSystemDisplayNumber(
                string.IsNullOrWhiteSpace(monitor.DeviceName) ? monitor.Id : monitor.DeviceName) ?? int.MaxValue)
            .ThenBy(monitor => monitor.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string ResolvePersistentId(string deviceName, string deviceId, string deviceKey, int fallbackIndex = 0)
    {
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            return deviceId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(deviceKey))
        {
            return deviceKey.Trim();
        }

        return string.IsNullOrWhiteSpace(deviceName) ? $"Monitor{fallbackIndex}" : deviceName.Trim();
    }

    public static string BuildDisplayName(string deviceName, string friendlyName)
    {
        string cleanDeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Display" : deviceName.Trim();
        string cleanFriendlyName = TrimParenthesizedDescription(friendlyName);
        if (!string.IsNullOrWhiteSpace(cleanFriendlyName) && !string.Equals(cleanFriendlyName, cleanDeviceName, StringComparison.OrdinalIgnoreCase))
        {
            return cleanFriendlyName;
        }

        const string displayPrefix = @"\\.\DISPLAY";
        return cleanDeviceName.StartsWith(displayPrefix, StringComparison.OrdinalIgnoreCase)
            ? $"Display {cleanDeviceName[displayPrefix.Length..]}"
            : cleanDeviceName;
    }

    private static string TrimParenthesizedDescription(string value)
    {
        string cleanValue = value.Trim();
        int descriptionStart = cleanValue.IndexOf('(');
        return descriptionStart > 0 ? cleanValue[..descriptionStart].Trim() : cleanValue;
    }

    private static int? GetSystemDisplayNumber(string deviceName)
    {
        const string displayPrefix = @"\\.\DISPLAY";
        string cleanDeviceName = deviceName.Trim();
        if (!cleanDeviceName.StartsWith(displayPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string suffix = cleanDeviceName[displayPrefix.Length..];
        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int displayNumber)
            ? displayNumber
            : null;
    }

    private static DisplayIdentity GetDisplayIdentity(string deviceName)
    {
        string adapterDeviceName = GetAdapterDeviceName(deviceName);
        var displayDevice = new NativeMethods.DISPLAY_DEVICE
        {
            cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>(),
        };

        if (NativeMethods.EnumDisplayDevices(
            adapterDeviceName,
            0,
            ref displayDevice,
            NativeMethods.EDD_GET_DEVICE_INTERFACE_NAME))
        {
            return new DisplayIdentity(displayDevice.DeviceString, displayDevice.DeviceID, displayDevice.DeviceKey);
        }

        displayDevice = new NativeMethods.DISPLAY_DEVICE
        {
            cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>(),
        };

        if (NativeMethods.EnumDisplayDevices(deviceName, 0, ref displayDevice, 0))
        {
            return new DisplayIdentity(displayDevice.DeviceString, displayDevice.DeviceID, displayDevice.DeviceKey);
        }

        return new DisplayIdentity(string.Empty, string.Empty, string.Empty);
    }

    private static string GetAdapterDeviceName(string deviceName)
    {
        for (uint index = 0; ; index++)
        {
            var displayAdapter = new NativeMethods.DISPLAY_DEVICE
            {
                cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.DISPLAY_DEVICE>(),
            };

            if (!NativeMethods.EnumDisplayDevices(null, index, ref displayAdapter, 0))
            {
                break;
            }

            if (string.Equals(displayAdapter.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
            {
                return displayAdapter.DeviceName;
            }
        }

        return deviceName;
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

    private readonly record struct DisplayIdentity(string FriendlyName, string DeviceId, string DeviceKey);
}
