using System.Text.RegularExpressions;

namespace SlideShowWallpaper.Tests;

public sealed class WallpaperPlaybackCoordinatorSourceTests
{
    [Fact]
    public void ConfigureTimer_WithSingleLoop_ReturnsBeforeStartingTimer()
    {
        string source = ReadCoordinatorWindowingSource();

        Assert.Matches(
            new Regex(
                "timer\\.Stop\\(\\);\\s*if \\(profile\\.PlaybackOrder == PlaybackOrder\\.SingleLoop\\)\\s*\\{\\s*return;\\s*\\}.*timer\\.Start\\(\\);",
                RegexOptions.Singleline),
            source);
    }

    [Fact]
    public void RestartTimer_WithSingleLoop_DoesNotRestartTimer()
    {
        string source = ReadCoordinatorWindowingSource();

        Assert.Matches(
            new Regex(
                "\\|\\| profile\\.PlaybackOrder == PlaybackOrder\\.SingleLoop\\s*\\|\\| !_timers\\.TryGetValue.*\\{\\s*return;\\s*\\}.*timer\\.Stop\\(\\);\\s*timer\\.Start\\(\\);",
                RegexOptions.Singleline),
            source);
    }

    [Fact]
    public void EnsureWindow_ConfiguresVideoCoverageTimerAfterCreatingWindow()
    {
        string source = ReadCoordinatorWindowingSource();
        string method = ExtractMethod(source, "private void EnsureWindow", "private async Task<bool> TryShowSelectedImageAsync");

        Assert.Contains("_windows[profile.Id] = window;", method);
        Assert.DoesNotContain("window.HardwareOverlayMoved += Window_HardwareOverlayMoved;", method);
        Assert.Contains("ConfigureVideoCoverageTimer();", method);
    }

    [Fact]
    public void HardwareOverlay_UsesSeparateOverlayWindow()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string refreshMethod = ExtractMethod(source, "private async Task RefreshHardwareOverlayAsync", "private void ClearHardwareOverlay");

        Assert.Contains("HardwareOverlayWindow? _hardwareOverlayWindow", source);
        Assert.Contains("EnsureHardwareOverlayWindow()", refreshMethod);
        Assert.Contains("overlayWindow.SetHardwareOverlay(state, monitorRect)", refreshMethod);
        Assert.DoesNotContain("HostOverlayOnDesktop", refreshMethod);
        Assert.DoesNotContain("SetDesktopHostOrigin", refreshMethod);
        Assert.DoesNotContain("window.SetHardwareOverlay(state)", refreshMethod);
        Assert.Contains("_hardwareOverlayWindow?.HideOverlay();", source);
    }

    [Fact]
    public void HardwareOverlayTimer_StartsAndStopsBrokerWithHardwareMonitor()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string configureMethod = ExtractMethod(source, "private void ConfigureHardwareOverlayTimer", "private async Task RefreshHardwareOverlayAsync");
        string brokerMethod = ExtractMethod(source, "private void ConfigureHardwareBroker", "private async Task RefreshHardwareOverlayAsync");
        string stopMethod = ExtractMethod(source, "public void StopPlayback", "private void ConfigureHardwareOverlayTimer");

        Assert.Contains("ConfigureHardwareBroker();", configureMethod);
        Assert.Contains("_playbackEnabled && _hardwareMonitorConfig.IsEnabled", brokerMethod);
        Assert.Contains("_ = Task.Run(_hardwareMonitorService.StartBroker);", brokerMethod);
        Assert.Contains("_ = Task.Run(_hardwareMonitorService.StopBroker);", brokerMethod);
        Assert.Contains("_ = Task.Run(_hardwareMonitorService.StopBroker);", stopMethod);
    }

    [Fact]
    public void RefreshHardwareOverlayAsync_SkipsSensorRefreshWhenTargetMonitorIsCovered()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string refreshMethod = ExtractMethod(source, "private async Task RefreshHardwareOverlayAsync", "private void ClearHardwareOverlay");
        int targetIndex = refreshMethod.IndexOf("string? targetMonitorId = GetHardwareOverlayTargetMonitorId();", StringComparison.Ordinal);
        int pauseIndex = refreshMethod.IndexOf("ShouldPauseHardwareOverlayRefresh(targetMonitorId, monitorRect)", StringComparison.Ordinal);
        int snapshotIndex = refreshMethod.IndexOf("_hardwareMonitorService.GetSnapshot", StringComparison.Ordinal);

        Assert.True(targetIndex >= 0);
        Assert.True(pauseIndex > targetIndex);
        Assert.True(snapshotIndex > pauseIndex);
        Assert.Contains("return;", refreshMethod[pauseIndex..snapshotIndex]);
    }

    [Fact]
    public void ShouldPauseHardwareOverlayRefresh_UsesForegroundCoveragePolicy()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "private bool ShouldPauseHardwareOverlayRefresh", "private void ClearHardwareOverlay");

        Assert.Contains("GetCoverageForegroundWindowInfo()", method);
        Assert.Contains("profile.PauseVideoWhenOtherAppMaximized", method);
        Assert.Contains("WindowCoveragePolicy.ShouldPauseVideo", method);
    }

    [Fact]
    public void CoverageForeground_PreservesExternalForegroundWhenOverlayBecomesForeground()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "private ForegroundWindowInfo? GetCoverageForegroundWindowInfo", "private void RememberExternalForegroundWindow");

        Assert.Contains("_foregroundWindowService.GetForegroundWindowInfo()", method);
        Assert.Contains("foregroundWindow.ProcessId != Environment.ProcessId", method);
        Assert.Contains("GetLastExternalForegroundWindowInfo()", method);
    }

    [Fact]
    public void HardwareOverlayWindow_IsShownWithoutActivating()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "private HardwareOverlayWindow EnsureHardwareOverlayWindow", "private void CloseHardwareOverlayWindow");

        Assert.DoesNotContain("_hardwareOverlayWindow.Activate();", method);
        Assert.DoesNotContain(".Activate();", method);
    }

    [Fact]
    public void HardwareOverlayMoved_UpdatesConfigAndPublishesPosition()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = source[source.IndexOf("private void Window_HardwareOverlayMoved", StringComparison.Ordinal)..];

        Assert.Contains("_hardwareMonitorConfig.X = HardwareEditorLayoutService.QuantizeCoordinate(args.X, double.MaxValue);", method);
        Assert.Contains("_hardwareMonitorConfig.Y = HardwareEditorLayoutService.QuantizeCoordinate(args.Y, double.MaxValue);", method);
        Assert.Contains("HardwareOverlayMoved?.Invoke", method);
    }

    [Fact]
    public void HideWindow_HidesWallpaperInsteadOfClosingWindow()
    {
        string source = ReadCoordinatorWindowingSource();
        string method = ExtractMethod(source, "private void HideWindow", "private static void CloseWindowSafely");

        Assert.Contains("window.HideWallpaperWindow();", method);
        Assert.DoesNotContain("window.Close();", method);
        Assert.Contains("ConfigureVideoCoverageTimer();", method);
    }

    [Fact]
    public void ApplyVideoCoverageState_OnlyTouchesShowingWallpaperWindows()
    {
        string source = ReadCoordinatorWindowingSource();
        string method = ExtractMethod(source, "private void ApplyVideoCoverageState", "private void RestartTimer");

        Assert.Contains("window.IsShowingWallpaper", method);
    }

    [Fact]
    public void DisplayPowerResume_RehostsActiveWindows()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "public void SetDisplayPowerVideoPause", "public void PauseOrResume");

        Assert.Contains("RehostActiveWindows();", method);
        Assert.Contains("_desktopHostService.HostOnDesktop(window, monitorId, _monitorRects);", method);
    }

    [Fact]
    public void ManualShowImage_DoesNotApplyExistingWindowProfileBeforeMediaSwitch()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "public async Task ShowImageAsync", "public void Shutdown");

        Assert.Contains("EnsureWindow(profile, applyProfile: false);", method);
        Assert.DoesNotContain("EnsureWindow(profile);", method);
    }

    [Fact]
    public void QueueReloads_UseRecursiveSubdirectorySetting()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.Queue.cs"));

        Assert.Contains("profile.IncludeSubdirectories", source);
        Assert.Contains("_folderChangeWatcherService.Watch(profile.Id, profile.FolderPath, profile.IncludeSubdirectories", source);
        Assert.Contains("GetOrLoadOrderedImagesWithStatusAsync", source);
        Assert.Contains("ReloadOrderedImagesAsync", source);
    }

    [Fact]
    public void CoordinatorExposesManualFolderRefresh()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.Queue.cs"));

        Assert.Contains("public void RefreshFolder(string monitorId)", source);
        Assert.Contains("DispatchFolderChange(monitorId)", source);
    }

    private static string ReadCoordinatorWindowingSource()
    {
        string root = FindProjectRoot();
        return File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.Windowing.cs"));
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        int start = source.IndexOf(startMarker, StringComparison.Ordinal);
        int end = source.IndexOf(endMarker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.True(end > start);
        return source[start..end];
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SlideShowWallpaper.csproj")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Project root not found.");
    }
}
