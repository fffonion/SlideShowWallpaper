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
        int snapshotIndex = refreshMethod.IndexOf("_hardwareMonitorService.GetSnapshot(_hardwareMonitorConfig)", StringComparison.Ordinal);

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

        Assert.Contains("GetCoverageWindowInfos()", method);
        Assert.Contains("profile.PauseVideoWhenOtherAppMaximized", method);
        Assert.Contains("WindowCoveragePolicy.ShouldPauseVideo", method);
    }

    [Fact]
    public void ApplyVideoCoverageState_UsesVisibleCoverageWindows()
    {
        string source = ReadCoordinatorWindowingSource();
        string method = ExtractMethod(source, "private void ApplyVideoCoverageState", "private void RestartTimer");

        Assert.Contains("IReadOnlyList<ForegroundWindowInfo> coverageWindows = GetCoverageWindowInfos();", method);
        Assert.Contains("profile.PauseVideoWhenOtherAppMaximized ? coverageWindows : []", method);
        Assert.DoesNotContain("ForegroundWindowInfo? foregroundWindow = GetCoverageForegroundWindowInfo();", method);
    }

    [Fact]
    public void CoverageWindows_EnumeratesVisibleWindowsAndKeepsForegroundFallback()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "private IReadOnlyList<ForegroundWindowInfo> GetCoverageWindowInfos", "private ForegroundWindowInfo? GetCoverageForegroundWindowInfo");

        Assert.Contains("_foregroundWindowService.GetVisibleWindowInfos()", method);
        Assert.Contains("GetCoverageForegroundWindowInfo()", method);
        Assert.Contains("visibleWindows.Any(window => window.Hwnd == foregroundWindow.Hwnd)", method);
        Assert.Contains("return [foregroundWindow, .. visibleWindows];", method);
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
    public void DisplayPowerResume_RestoresCurrentMediaAndSchedulesRecovery()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "public void SetDisplayPowerVideoPause", "public void PauseOrResume");

        Assert.Contains("RecoverWallpapersAfterDisplayResume(ShouldRefreshMediaForDisplayResume());", method);
        Assert.Contains("ScheduleDisplayResumeRecovery();", method);
        Assert.Contains("RefreshFoldersAfterDisplayResume();", method);
    }

    [Fact]
    public void DisplayConfigurationChange_UsesSameRecoveryPathWithMediaRefreshCooldown()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "public void RecoverWallpapersAfterDisplayConfigurationChange", "private void RecoverWallpapersAfterDisplayResume");
        string cooldownMethod = ExtractMethod(source, "private bool ShouldRefreshMediaForDisplayResume", "private void ConfigureHardwareOverlayTimer");

        Assert.Contains("RecoverWallpapersAfterDisplayResume(ShouldRefreshMediaForDisplayResume());", method);
        Assert.Contains("ScheduleDisplayResumeRecovery();", method);
        Assert.Contains("RefreshFoldersAfterDisplayResume();", method);
        Assert.Contains("DisplayResumeMediaRefreshCooldown", cooldownMethod);
        Assert.Contains("_lastDisplayResumeMediaRefreshUtc", cooldownMethod);
    }

    [Fact]
    public void DisplayResumeRecovery_RehostsWindowsWithoutAdvancingQueue()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "private void RecoverWallpapersAfterDisplayResume", "private async Task ShowCurrentOrNextAsync");
        string showCurrentMethod = ExtractMethod(source, "private async Task ShowCurrentOrNextAsync", "private void ScheduleDisplayResumeRecovery");

        Assert.Contains("window.ShowWallpaperWindow();", method);
        Assert.Contains("_desktopHostService.HostOnDesktop(window, profile.Id, _monitorRects);", method);
        Assert.Contains("ShowCurrentOrNextAsync(profile, window, playbackQueue)", method);
        Assert.Contains("ImagePlaybackItem? currentItem = queue.CurrentItem;", showCurrentMethod);
        Assert.Contains("ShowWindowMediaSafeAsync(profile.Id, window, currentItem)", showCurrentMethod);
        Assert.Contains("await ShowNextAsync(profile.Id);", showCurrentMethod);
    }

    [Fact]
    public void DisplayResumeRecovery_DefersMediaUntilPhysicalMonitorReturns()
    {
        string source = File.ReadAllText(Path.Combine(FindProjectRoot(), "Services", "WallpaperPlaybackCoordinator.cs"));
        string method = ExtractMethod(source, "private void RecoverWallpapersAfterDisplayResume", "private async Task ShowCurrentOrNextAsync");

        Assert.Contains("_displayMediaRecovery.Request(", method);
        Assert.Contains("_displayMediaRecovery.Take(profile.Id, isMonitorAvailable: true)", method);
        Assert.True(method.IndexOf("if (!_monitorRects.ContainsKey(profile.Id))", StringComparison.Ordinal)
            < method.IndexOf("EnsureWindow(profile);", StringComparison.Ordinal));
        Assert.Contains("!window.IsClosed", method);
        Assert.Contains("timer.Stop();", method);
    }

    [Fact]
    public void SessionReconnect_RegistersAndClearsStalePowerPauseThroughRecoveryPath()
    {
        string root = FindProjectRoot();
        string tray = File.ReadAllText(Path.Combine(root, "Services", "TrayIconService.cs"));
        string power = File.ReadAllText(Path.Combine(root, "Services", "DisplayPowerPauseService.cs"));
        string resume = ExtractMethod(power, "public void ResumeAfterSessionReconnect", "private void Register(");

        Assert.Contains("WTSRegisterSessionNotification(_windowHandle, 0)", tray);
        Assert.Contains("WTSUnRegisterSessionNotification(_windowHandle)", tray);
        Assert.Contains("NativeMethods.WM_WTSSESSION_CHANGE", tray);
        Assert.Contains("DesktopSessionRecoveryPolicy.ShouldResumeDisplay(sessionEvent)", tray);
        Assert.Contains("ResumeAfterSessionReconnect();", tray);
        Assert.Contains("_hasPausedSinceStartup = false;", resume);
        Assert.Contains("_pauseStateChanged(false);", resume);
    }

    [Fact]
    public void FolderAvailabilityRetry_RestoresCurrentMediaAfterFolderReturns()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.Queue.cs"));
        string method = ExtractMethod(source, "private void RetryUnavailableFolder", "private void StopFolderAvailabilityRetry");

        Assert.Contains("DispatchFolderChange(monitorId);", method);
        Assert.Contains("RecoverWallpapersAfterDisplayResume(refreshMedia: true);", method);
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
    public void QueueReloads_PreserveCurrentQueueWhenFolderIsTemporarilyUnavailable()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.Queue.cs"));
        string method = ExtractMethod(source, "private async void ReloadChangedFolderAsync", "private void RefreshFoldersAfterDisplayResume");

        int unavailableIndex = method.IndexOf("if (IsFolderTemporarilyUnavailable(profile))", StringComparison.Ordinal);
        int retryIndex = method.IndexOf("ScheduleFolderAvailabilityRetry(profile.Id);", StringComparison.Ordinal);
        int scanIndex = method.IndexOf("_imageOrderService.ReloadOrderedImagesAsync", StringComparison.Ordinal);
        Assert.True(unavailableIndex >= 0);
        Assert.True(retryIndex > unavailableIndex);
        Assert.True(scanIndex > retryIndex);
        Assert.Contains("return;", method[retryIndex..scanIndex]);
        Assert.DoesNotContain("ReplaceQueueAfterCurrent(profile, []", method);
    }

    [Fact]
    public void FolderAvailabilityRetry_RunsAfterDisplayResumeAndStopsOnShutdown()
    {
        string root = FindProjectRoot();
        string queueSource = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.Queue.cs"));
        string coordinatorSource = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string retryMethod = ExtractMethod(queueSource, "private void RetryUnavailableFolder", "private void StopFolderAvailabilityRetry");
        string stopMethod = ExtractMethod(coordinatorSource, "public void StopPlayback", "private void ConfigureHardwareOverlayTimer");

        Assert.Contains("RefreshFoldersAfterDisplayResume", queueSource);
        Assert.Contains("ScheduleFolderAvailabilityRetry", queueSource);
        Assert.Contains("DispatchFolderChange(monitorId);", retryMethod);
        Assert.Contains("StopAllFolderAvailabilityRetries();", stopMethod);
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
