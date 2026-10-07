namespace SlideShowWallpaper.Services;

public static class DesktopSessionRecoveryPolicy
{
    private const int ConsoleConnect = 1;
    private const int RemoteConnect = 3;
    private const int RemoteDisconnect = 4;
    private const int SessionUnlock = 8;
    private const int DesktopReady = 15;

    public static bool ShouldResumeDisplay(int sessionEvent) => sessionEvent is ConsoleConnect or RemoteConnect or SessionUnlock or DesktopReady;

    public static bool ShouldRetryDisplay(int sessionEvent) => sessionEvent == RemoteDisconnect;
}
