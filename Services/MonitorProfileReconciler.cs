using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed record MonitorProfileReconciliation(
    IReadOnlyList<MonitorProfile> Profiles,
    IReadOnlyDictionary<string, string> MonitorIdChanges);

public static class MonitorProfileReconciler
{
    private const string LegacyDisplayPrefix = @"\\.\DISPLAY";

    public static MonitorProfileReconciliation Reconcile(
        IReadOnlyList<MonitorProfile> currentProfiles,
        IReadOnlyList<MonitorProfile> savedProfiles)
    {
        var unmatchedSavedProfiles = savedProfiles.ToList();
        var reconciledProfiles = new List<MonitorProfile>(currentProfiles.Count);
        var idChanges = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (MonitorProfile currentProfile in currentProfiles)
        {
            MonitorProfile? savedProfile = FindSavedProfile(currentProfile, currentProfiles, unmatchedSavedProfiles);
            if (savedProfile is null)
            {
                reconciledProfiles.Add(currentProfile);
                continue;
            }

            unmatchedSavedProfiles.Remove(savedProfile);
            string previousId = savedProfile.Id;
            savedProfile.Id = currentProfile.Id;
            savedProfile.DeviceName = currentProfile.DeviceName;
            savedProfile.DisplayName = currentProfile.DisplayName;
            reconciledProfiles.Add(savedProfile);

            if (!string.Equals(previousId, currentProfile.Id, StringComparison.OrdinalIgnoreCase))
            {
                idChanges[previousId] = currentProfile.Id;
            }
        }

        return new MonitorProfileReconciliation(reconciledProfiles, idChanges);
    }

    private static MonitorProfile? FindSavedProfile(
        MonitorProfile currentProfile,
        IReadOnlyList<MonitorProfile> currentProfiles,
        IReadOnlyList<MonitorProfile> savedProfiles)
    {
        MonitorProfile? identityMatch = savedProfiles.FirstOrDefault(savedProfile =>
            !IsLegacyDisplayId(savedProfile.Id)
            && string.Equals(savedProfile.Id, currentProfile.Id, StringComparison.OrdinalIgnoreCase));
        if (identityMatch is not null)
        {
            return identityMatch;
        }

        if (HasUniqueFriendlyName(currentProfile, currentProfiles, savedProfiles))
        {
            return savedProfiles.First(savedProfile =>
                string.Equals(savedProfile.DisplayName, currentProfile.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        return savedProfiles.FirstOrDefault(savedProfile =>
            IsLegacyDisplayId(savedProfile.Id)
            && string.Equals(savedProfile.Id, currentProfile.DeviceName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasUniqueFriendlyName(
        MonitorProfile currentProfile,
        IReadOnlyList<MonitorProfile> currentProfiles,
        IReadOnlyList<MonitorProfile> savedProfiles)
    {
        if (string.IsNullOrWhiteSpace(currentProfile.DisplayName)
            || string.Equals(
                currentProfile.DisplayName,
                MonitorService.BuildDisplayName(currentProfile.DeviceName, string.Empty),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int currentNameCount = currentProfiles.Count(profile =>
            string.Equals(profile.DisplayName, currentProfile.DisplayName, StringComparison.OrdinalIgnoreCase));
        int savedNameCount = savedProfiles.Count(profile =>
            string.Equals(profile.DisplayName, currentProfile.DisplayName, StringComparison.OrdinalIgnoreCase));
        return currentNameCount == 1 && savedNameCount == 1;
    }

    private static bool IsLegacyDisplayId(string monitorId) =>
        monitorId.StartsWith(LegacyDisplayPrefix, StringComparison.OrdinalIgnoreCase);
}
