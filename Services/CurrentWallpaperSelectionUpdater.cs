using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public static class CurrentWallpaperSelectionUpdater
{
    public static bool Update(IReadOnlyList<MonitorProfile> profiles, string monitorId, string imagePath)
    {
        if (string.IsNullOrWhiteSpace(monitorId) || string.IsNullOrWhiteSpace(imagePath))
        {
            return false;
        }

        MonitorProfile? profile = profiles.FirstOrDefault(item => string.Equals(item.Id, monitorId, StringComparison.OrdinalIgnoreCase));
        if (profile is null || string.Equals(profile.SelectedImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        profile.SelectedImagePath = imagePath;
        return true;
    }
}
