using System.Runtime.InteropServices;
using SlideShowWallpaper.Interop;

namespace SlideShowWallpaper.Services;

internal sealed class DisplayPowerPauseService : IDisposable
{
    private readonly Action<bool> _pauseStateChanged;
    private readonly List<IntPtr> _registrations = [];
    private bool _disposed;

    public DisplayPowerPauseService(IntPtr windowHandle, Action<bool> pauseStateChanged)
    {
        _pauseStateChanged = pauseStateChanged;
        Register(windowHandle, DisplayPowerPausePolicy.ConsoleDisplayStateGuid);
        Register(windowHandle, DisplayPowerPausePolicy.MonitorPowerOnGuid);
    }

    public void HandlePowerBroadcast(IntPtr wParam, IntPtr lParam)
    {
        uint powerEvent = unchecked((uint)wParam.ToInt64());
        Guid? powerSetting = null;
        int? data = null;

        if (powerEvent == NativeMethods.PBT_POWERSETTINGCHANGE && lParam != IntPtr.Zero)
        {
            ReadPowerBroadcastSetting(lParam, out powerSetting, out data);
        }

        bool? pauseState = DisplayPowerPausePolicy.GetPauseState(powerEvent, powerSetting, data);
        if (pauseState.HasValue)
        {
            _pauseStateChanged(pauseState.Value);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (IntPtr registration in _registrations)
        {
            NativeMethods.UnregisterPowerSettingNotification(registration);
        }

        _registrations.Clear();
        _disposed = true;
    }

    private void Register(IntPtr windowHandle, Guid powerSettingGuid)
    {
        Guid guid = powerSettingGuid;
        IntPtr registration = NativeMethods.RegisterPowerSettingNotification(
            windowHandle,
            ref guid,
            NativeMethods.DEVICE_NOTIFY_WINDOW_HANDLE);
        if (registration == IntPtr.Zero)
        {
            AppLog.Write($"RegisterPowerSettingNotification failed for {powerSettingGuid}: {Marshal.GetLastWin32Error()}");
            return;
        }

        _registrations.Add(registration);
    }

    private static void ReadPowerBroadcastSetting(IntPtr lParam, out Guid? powerSetting, out int? data)
    {
        try
        {
            NativeMethods.POWERBROADCAST_SETTING setting = Marshal.PtrToStructure<NativeMethods.POWERBROADCAST_SETTING>(lParam);
            powerSetting = setting.PowerSetting;
            data = setting.DataLength >= sizeof(int)
                ? Marshal.ReadInt32(lParam, Marshal.SizeOf<NativeMethods.POWERBROADCAST_SETTING>())
                : null;
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            powerSetting = null;
            data = null;
        }
    }
}
