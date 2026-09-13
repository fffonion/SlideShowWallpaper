using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper.Tests;

public sealed class MonitorProfileReconcilerTests
{
    [Fact]
    public void Reconcile_WithPersistentIds_PreservesSettingsByPhysicalMonitor()
    {
        MonitorProfile[] currentProfiles =
        [
            CreateCurrent("path-b", @"\\.\DISPLAY1", "Philips"),
            CreateCurrent("path-a", @"\\.\DISPLAY2", "AOC"),
        ];
        MonitorProfile[] savedProfiles =
        [
            new() { Id = "path-a", DisplayName = "Old AOC", FolderPath = @"C:\AOC" },
            new() { Id = "path-b", DisplayName = "Old Philips", FolderPath = @"C:\Philips" },
        ];

        MonitorProfileReconciliation result = MonitorProfileReconciler.Reconcile(currentProfiles, savedProfiles);

        Assert.Collection(
            result.Profiles,
            profile => Assert.Equal(@"C:\Philips", profile.FolderPath),
            profile => Assert.Equal(@"C:\AOC", profile.FolderPath));
        Assert.Empty(result.MonitorIdChanges);
    }

    [Fact]
    public void Reconcile_WithLegacyIdsAndSwappedNumbers_MatchesUniqueFriendlyNames()
    {
        MonitorProfile[] currentProfiles =
        [
            CreateCurrent("path-b", @"\\.\DISPLAY1", "Philips"),
            CreateCurrent("path-a", @"\\.\DISPLAY2", "AOC"),
        ];
        MonitorProfile[] savedProfiles =
        [
            new() { Id = @"\\.\DISPLAY1", DisplayName = "AOC", FolderPath = @"C:\AOC" },
            new() { Id = @"\\.\DISPLAY2", DisplayName = "Philips", FolderPath = @"C:\Philips" },
        ];

        MonitorProfileReconciliation result = MonitorProfileReconciler.Reconcile(currentProfiles, savedProfiles);

        Assert.Collection(
            result.Profiles,
            profile =>
            {
                Assert.Equal("path-b", profile.Id);
                Assert.Equal(@"C:\Philips", profile.FolderPath);
            },
            profile =>
            {
                Assert.Equal("path-a", profile.Id);
                Assert.Equal(@"C:\AOC", profile.FolderPath);
            });
        Assert.Equal("path-a", result.MonitorIdChanges[@"\\.\DISPLAY1"]);
        Assert.Equal("path-b", result.MonitorIdChanges[@"\\.\DISPLAY2"]);
    }

    [Fact]
    public void Reconcile_WithLegacyIdAndNoFriendlyName_FallsBackToCurrentDeviceName()
    {
        MonitorProfile currentProfile = CreateCurrent("path-a", @"\\.\DISPLAY2", "Display 2");
        var savedProfile = new MonitorProfile
        {
            Id = @"\\.\DISPLAY2",
            DisplayName = "Display 2",
            FolderPath = @"C:\Fallback",
        };

        MonitorProfileReconciliation result = MonitorProfileReconciler.Reconcile([currentProfile], [savedProfile]);

        MonitorProfile profile = Assert.Single(result.Profiles);
        Assert.Equal("path-a", profile.Id);
        Assert.Equal(@"C:\Fallback", profile.FolderPath);
    }

    [Fact]
    public void Reconcile_WithNoSavedMatch_UsesCurrentProfileDefaults()
    {
        MonitorProfile currentProfile = CreateCurrent("path-a", @"\\.\DISPLAY1", "AOC");

        MonitorProfileReconciliation result = MonitorProfileReconciler.Reconcile([currentProfile], []);

        Assert.Same(currentProfile, Assert.Single(result.Profiles));
        Assert.Empty(result.MonitorIdChanges);
    }

    private static MonitorProfile CreateCurrent(string id, string deviceName, string displayName) => new()
    {
        Id = id,
        DeviceName = deviceName,
        DisplayName = displayName,
    };
}
