using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.ViewModels;

public sealed class MonitorSettingsViewModel
{
    public MonitorSettingsViewModel(MonitorProfile profile)
    {
        Profile = profile;
    }

    public MonitorProfile Profile { get; }
}
