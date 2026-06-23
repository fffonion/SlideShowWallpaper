namespace SlideShowWallpaper.Services;

public static class DisplayPowerPausePolicy
{
    public static readonly Guid ConsoleDisplayStateGuid = new("6FE69556-704A-47A0-8F24-C28D936FDA47");
    public static readonly Guid MonitorPowerOnGuid = new("02731015-4510-4526-99E6-E5A17EBD1AEA");

    public static bool? GetPauseState(uint powerEvent, Guid? powerSetting = null, int? data = null)
    {
        return powerEvent switch
        {
            Interop.NativeMethods.PBT_APMSUSPEND => true,
            Interop.NativeMethods.PBT_APMRESUMEAUTOMATIC or Interop.NativeMethods.PBT_APMRESUMESUSPEND => false,
            Interop.NativeMethods.PBT_POWERSETTINGCHANGE => GetPauseStateFromPowerSetting(powerSetting, data),
            _ => null,
        };
    }

    private static bool? GetPauseStateFromPowerSetting(Guid? powerSetting, int? data)
    {
        if (powerSetting is null || data is null)
        {
            return null;
        }

        if (powerSetting == ConsoleDisplayStateGuid)
        {
            return data.Value switch
            {
                0 => true,
                1 => false,
                _ => null,
            };
        }

        if (powerSetting == MonitorPowerOnGuid)
        {
            return data.Value switch
            {
                0 => true,
                1 => false,
                _ => null,
            };
        }

        return null;
    }
}
