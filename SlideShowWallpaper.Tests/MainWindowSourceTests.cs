namespace SlideShowWallpaper.Tests;

public sealed class MainWindowSourceTests
{
    [Fact]
    public void Constructor_WithStartInTray_SchedulesBackgroundMemoryTrimAfterApplyingSettings()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        string constructor = source[
            source.IndexOf("public MainWindow(", StringComparison.Ordinal)..
            source.IndexOf("private void HandleDisplayPowerPauseChanged", StringComparison.Ordinal)];
        int applySettingsIndex = constructor.IndexOf("ApplySettings();", StringComparison.Ordinal);
        int trimIndex = constructor.IndexOf("if (startInTray)", applySettingsIndex, StringComparison.Ordinal);

        Assert.True(applySettingsIndex >= 0);
        Assert.True(trimIndex > applySettingsIndex);
        Assert.Contains("_backgroundStartupTrimPending = true;", constructor[trimIndex..]);
        Assert.Contains("ScheduleBackgroundMemoryTrim(BackgroundStartupTrimDelay);", constructor[trimIndex..]);
        Assert.DoesNotContain("TrimBackgroundMemory();", constructor[trimIndex..]);
    }

    [Fact]
    public void BackgroundStartupTrimDelays_AllowWallpaperAndBrokerStartupToSettle()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));

        Assert.Contains("BackgroundStartupTrimDelay = TimeSpan.FromSeconds(30)", source);
        Assert.Contains("BackgroundWallpaperReadyTrimDelay = TimeSpan.FromSeconds(30)", source);
        Assert.Contains("BackgroundBrokerReadyTrimDelay = TimeSpan.FromSeconds(30)", source);
    }

    [Fact]
    public void Constructor_SubscribesHardwareBrokerStartupForBackgroundMemoryTrim()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        string constructor = source[
            source.IndexOf("public MainWindow(", StringComparison.Ordinal)..
            source.IndexOf("private void HardwareMonitorService_BrokerProcessStarted", StringComparison.Ordinal)];
        string windowingSource = File.ReadAllText(Path.Combine(root, "MainWindow.Windowing.cs"));
        string shutdownMethod = windowingSource[
            windowingSource.IndexOf("private void ShutdownApplication", StringComparison.Ordinal)..];

        Assert.Contains("_hardwareMonitorService.BrokerProcessStarted += HardwareMonitorService_BrokerProcessStarted;", constructor);
        Assert.Contains("_hardwareMonitorService.BrokerProcessStarted -= HardwareMonitorService_BrokerProcessStarted;", shutdownMethod);
    }

    [Fact]
    public void HardwareMonitorServiceBrokerProcessStarted_ReschedulesPendingBackgroundTrim()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        string method = source[
            source.IndexOf("private void HardwareMonitorService_BrokerProcessStarted", StringComparison.Ordinal)..
            source.IndexOf("private void HandleDisplayPowerPauseChanged", StringComparison.Ordinal)];

        Assert.Contains("_backgroundStartupTrimPending && _settingsUiUnloadedForBackground", method);
        Assert.Contains("ScheduleBackgroundMemoryTrim(BackgroundBrokerReadyTrimDelay);", method);
    }

    [Fact]
    public void ProgramMain_StartsWinUiWithoutBrokerMode()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Program.cs"));
        string method = source[
            source.IndexOf("public static int Main", StringComparison.Ordinal)..
            source.LastIndexOf('}')];

        int startIndex = method.IndexOf("WinUiAppHost.Start();", StringComparison.Ordinal);

        Assert.True(startIndex >= 0);
        Assert.DoesNotContain("HardwareMonitorBrokerHost", method);
        Assert.DoesNotContain("Microsoft.UI", source);
    }

    [Fact]
    public void Project_DisablesGeneratedXamlMain()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "SlideShowWallpaper.csproj"));

        Assert.Contains("DISABLE_XAML_GENERATED_MAIN", source);
    }

    [Fact]
    public void ProgramMain_HandlesElevationDemotionBeforeStartingWinUi()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Program.cs"));
        string method = source[
            source.IndexOf("public static int Main", StringComparison.Ordinal)..
            source.LastIndexOf('}')];

        int skipCheckIndex = method.IndexOf("!launchOptions.SkipElevationDemotion", StringComparison.Ordinal);
        int demoteIndex = method.IndexOf("RestartIfCurrentProcessIsElevated", StringComparison.Ordinal);
        int failedIndex = method.IndexOf("case UnelevatedRestartResult.Failed:", StringComparison.Ordinal);
        int startIndex = method.IndexOf("WinUiAppHost.Start();", StringComparison.Ordinal);

        Assert.True(skipCheckIndex >= 0);
        Assert.True(demoteIndex > skipCheckIndex);
        Assert.True(failedIndex > demoteIndex);
        Assert.True(startIndex > demoteIndex);
        Assert.True(startIndex > failedIndex);
        Assert.Contains("case UnelevatedRestartResult.Restarted:", method);
        Assert.Contains("return 1;", method);
        Assert.Contains("return 0;", method);
    }

    [Fact]
    public void ProgramMain_DoesNotStartWinUiWhenNoDemoteProcessIsStillElevated()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Program.cs"));
        string method = source[
            source.IndexOf("public static int Main", StringComparison.Ordinal)..
            source.LastIndexOf('}')];

        int guardIndex = method.IndexOf("launchOptions.SkipElevationDemotion && CurrentProcessPrivilege.IsElevated()", StringComparison.Ordinal);
        int startIndex = method.IndexOf("WinUiAppHost.Start();", StringComparison.Ordinal);

        Assert.True(guardIndex >= 0);
        Assert.True(startIndex > guardIndex);
        Assert.Contains("AppLog.Write(\"Process is still elevated after elevation demotion was marked complete.\");", method);
        Assert.Contains("return 1;", method[guardIndex..startIndex]);
    }

    [Fact]
    public void ProgramMain_AllowsDemotedProcessToStartWinUiAndElevatedBroker()
    {
        Services.LaunchOptions options = Services.LaunchOptions.FromArguments([
            Services.LaunchOptions.ElevatedBrokerArgument,
            Services.UnelevatedRestartService.NoDemoteArgument,
        ]);

        Assert.True(options.SkipElevationDemotion);
        Assert.True(options.StartHardwareBrokerElevated);
        Assert.True(options.AllowMultipleInstances);
    }

    [Fact]
    public void RestartHardwareBrokerAsAdministrator_RestartsOnlyHardwareBroker()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private void RestartHardwareBrokerAsAdministrator", StringComparison.Ordinal)..
            source.IndexOf("private static FrameworkElement CreateHardwareSensorSelectionContent", StringComparison.Ordinal)];

        Assert.Contains("_hardwareMonitorService.SetBrokerElevation(true);", method);
        Assert.Contains("_hardwareMonitorService.StopBroker();", method);
        Assert.Contains("RefreshHardwareSnapshot();", method);
        Assert.Contains("refreshRequested?.Invoke();", method);
        Assert.DoesNotContain("_administratorRestartService.TryRestart()", method);
        Assert.DoesNotContain("ExitApplication();", method);
        Assert.DoesNotContain("CurrentProcessPrivilege.IsAdministrator", method);
    }

    [Fact]
    public void RestartAsAdministrator_IsNotUsedByHardwareSensorDialog()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string dialogRegion = source[
            source.IndexOf("private async Task ShowHardwareSensorSelectionDialogAsync", StringComparison.Ordinal)..
            source.IndexOf("private static FrameworkElement CreateHardwareSensorSelectionContent", StringComparison.Ordinal)];

        Assert.DoesNotContain("RestartAsAdministrator", dialogRegion);
        Assert.DoesNotContain("_administratorRestartService.TryRestart()", dialogRegion);
    }

    [Fact]
    public void AppLaunch_ConfiguresHardwareBrokerElevationBeforeCreatingCoordinator()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "App.xaml.cs"));
        string method = source[
            source.IndexOf("protected override void OnLaunched", StringComparison.Ordinal)..
            source.IndexOf("private void ShowExistingInstanceWindow", StringComparison.Ordinal)];

        int optionsIndex = method.IndexOf("LaunchOptions.FromArguments", StringComparison.Ordinal);
        int elevationIndex = method.IndexOf("_hardwareMonitorService.SetBrokerElevation(launchOptions.StartHardwareBrokerElevated);", StringComparison.Ordinal);
        int coordinatorIndex = method.IndexOf("new WallpaperPlaybackCoordinator", StringComparison.Ordinal);

        Assert.True(optionsIndex >= 0);
        Assert.True(elevationIndex > optionsIndex);
        Assert.True(coordinatorIndex > elevationIndex);
    }

    [Fact]
    public void CurrentWallpaperChanged_WhenBackgroundStartupTrimIsPending_ReschedulesTrimAfterWallpaperReady()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Playback.cs"));
        string method = source[
            source.IndexOf("private void Coordinator_CurrentWallpaperChanged", StringComparison.Ordinal)..
            source.IndexOf("private async void PreviewList_SelectionChanged", StringComparison.Ordinal)];

        Assert.Contains("_backgroundStartupTrimPending && _settingsUiUnloadedForBackground", method);
        Assert.Contains("ScheduleBackgroundMemoryTrim(BackgroundWallpaperReadyTrimDelay);", method);
    }

    [Fact]
    public void HardwareOverlayMoved_SavesUpdatedMonitorPosition()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Playback.cs"));
        string method = source[
            source.IndexOf("private void Coordinator_HardwareOverlayMoved", StringComparison.Ordinal)..
            source.IndexOf("private async void PreviewList_SelectionChanged", StringComparison.Ordinal)];

        Assert.Contains("_viewModel.HardwareMonitor.X = HardwareEditorLayoutService.QuantizeCoordinate(args.X, double.MaxValue);", method);
        Assert.Contains("_viewModel.HardwareMonitor.Y = HardwareEditorLayoutService.QuantizeCoordinate(args.Y, double.MaxValue);", method);
        Assert.Contains("_settingsStore.Save(CreateConfig());", method);
    }

    [Fact]
    public void UnloadSettingsUiForBackground_UsesSharedBackgroundTrimMethod()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.SettingsState.cs"));
        string unloadMethod = source[
            source.IndexOf("private void UnloadSettingsUiForBackground", StringComparison.Ordinal)..
            source.IndexOf("private void EnsureSettingsUiLoaded", StringComparison.Ordinal)];

        Assert.Contains("TrimBackgroundMemory();", unloadMethod);
        Assert.DoesNotContain("ProcessMemoryTrimmer.TrimCurrentProcess();", unloadMethod);
    }

    [Fact]
    public void UnloadSettingsUiForBackground_ClearsFontCatalogCache()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.SettingsState.cs"));
        string unloadMethod = source[
            source.IndexOf("private void UnloadSettingsUiForBackground", StringComparison.Ordinal)..
            source.IndexOf("private void EnsureSettingsUiLoaded", StringComparison.Ordinal)];

        Assert.Contains("FontCatalogService.ClearCache();", unloadMethod);
    }

    [Fact]
    public void EnsureSettingsUiLoaded_CancelsPendingBackgroundStartupTrim()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.SettingsState.cs"));
        string method = source[
            source.IndexOf("private void EnsureSettingsUiLoaded", StringComparison.Ordinal)..
            source.IndexOf("private static void TrimBackgroundMemory", StringComparison.Ordinal)];

        Assert.Contains("_backgroundStartupTrimPending = false;", method);
        Assert.Contains("CancelBackgroundMemoryTrim();", method);
    }

    [Fact]
    public void ScheduleBackgroundMemoryTrim_UsesBackgroundDelayInsteadOfDispatcherTimer()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.SettingsState.cs"));
        string method = source[
            source.IndexOf("private void ScheduleBackgroundMemoryTrim", StringComparison.Ordinal)..
            source.IndexOf("private void RunPendingBackgroundStartupTrim", StringComparison.Ordinal)];

        Assert.Contains("RunBackgroundMemoryTrimAfterDelayAsync(delay, cancellation)", method);
        Assert.Contains("Task.Delay(delay, cancellation.Token).ConfigureAwait(false)", method);
        Assert.DoesNotContain("_backgroundMemoryTrimTimer", source);
        Assert.DoesNotContain("DispatcherQueue.TryEnqueue", method);
    }

    [Fact]
    public void RunPendingBackgroundStartupTrim_DoesNotDependOnWindowVisibilityFlag()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.SettingsState.cs"));
        string method = source[
            source.IndexOf("private void RunPendingBackgroundStartupTrim", StringComparison.Ordinal)..];

        Assert.Contains("if (!_backgroundStartupTrimPending)", method);
        Assert.DoesNotContain("_settingsUiUnloadedForBackground", method);
    }

    [Fact]
    public void HandleWindowMinimizedChanged_IgnoresRestoreMessagesWhileSettingsUiIsUnloaded()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Windowing.cs"));
        string method = source[
            source.IndexOf("private void HandleWindowMinimizedChanged", StringComparison.Ordinal)..
            source.IndexOf("private void ExitApplication", StringComparison.Ordinal)];
        int unloadedCheckIndex = method.IndexOf("if (_settingsUiUnloadedForBackground)", StringComparison.Ordinal);
        int ensureIndex = method.IndexOf("EnsureSettingsUiLoaded();", StringComparison.Ordinal);

        Assert.True(unloadedCheckIndex >= 0);
        Assert.True(ensureIndex > unloadedCheckIndex);
        Assert.Contains("return;", method[unloadedCheckIndex..ensureIndex]);
    }

    [Fact]
    public void BuildAppSettingsPage_WrapsContentInScrollViewer()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private UIElement BuildAppSettingsPage", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateAutostartControls", StringComparison.Ordinal)];

        Assert.Contains("new ScrollViewer", method);
        Assert.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Auto", method);
        Assert.Contains("HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled", method);
    }

    [Fact]
    public void BuildAppSettingsPage_ContainsHardwareMonitorSection()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private UIElement BuildAppSettingsPage", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateAutostartControls", StringComparison.Ordinal)];

        Assert.Contains("EnsureDefaultHardwareSensors(hardwareMonitorConfig)", method);
        Assert.Contains("CreateHardwareMonitorSettingsSection()", method);
        Assert.DoesNotContain("CreateHardwareSensorSelectionList", method);
    }

    [Fact]
    public void BuildAppSettingsPage_ContainsGitHubReleaseUpdateCheck()
    {
        string root = FindProjectRoot();
        string settingsSource = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string windowSource = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        string windowingSource = File.ReadAllText(Path.Combine(root, "MainWindow.Windowing.cs"));
        string method = settingsSource[
            settingsSource.IndexOf("private UIElement BuildAppSettingsPage", StringComparison.Ordinal)..
            settingsSource.IndexOf("private FrameworkElement CreateAutostartControls", StringComparison.Ordinal)];

        Assert.Contains("CreateUpdateCheckControls()", method);
        Assert.Contains("private readonly GitHubReleaseUpdateService _updateCheckService = new();", windowSource);
        Assert.Contains("private readonly AppUpdateInstallerService _updateInstallerService = new();", windowSource);
        Assert.Contains("private async Task CheckForUpdatesAsync()", settingsSource);
        Assert.Contains("AppVersionService.GetCurrentVersion()", settingsSource);
        Assert.Contains("_updateCheckService.CheckForUpdateAsync", settingsSource);
        Assert.Contains("DownloadAndInstallUpdateAsync", settingsSource);
        Assert.Contains("_updateInstallerService.PrepareUpdateAsync", settingsSource);
        Assert.Contains("_updateInstallerService.StartUpdater", settingsSource);
        Assert.Contains("_updateCheckButton.Visibility = Visibility.Collapsed", settingsSource);
        Assert.DoesNotContain("_updateReleaseButton", settingsSource);
        Assert.DoesNotContain("OpenExternalUpdateUri", settingsSource);
        Assert.Contains("_updateCheckService.Dispose();", windowingSource);
        Assert.Contains("_updateInstallerService.Dispose();", windowingSource);
    }

    [Fact]
    public void MediaFilterChoices_ContainPortraitAndLandscapeImageFilters()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        string choices = source[
            source.IndexOf("private static IReadOnlyList<Choice<PlaybackMediaFilter>> MediaFilterChoices", StringComparison.Ordinal)..
            source.IndexOf("private static IReadOnlyList<Choice<TimeUnit>> TimeUnitChoices", StringComparison.Ordinal)];

        Assert.Contains("PlaybackMediaFilter.PortraitImagesOnly", choices);
        Assert.Contains("MediaFilterPortraitImages", choices);
        Assert.Contains("PlaybackMediaFilter.LandscapeImagesOnly", choices);
        Assert.Contains("MediaFilterLandscapeImages", choices);
    }

    [Fact]
    public void BuildMonitorSettings_ContainsRecursiveSubdirectoryOption()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private UIElement BuildMonitorSettings", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement BuildMonitorCommandBar", StringComparison.Ordinal)];

        Assert.Contains("profile.IncludeSubdirectories", method);
        Assert.Contains("IncludeSubdirectories", method);
    }

    [Fact]
    public void BuildMonitorCommandBar_AddsRefreshButtonAfterShuffle()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement BuildMonitorCommandBar", StringComparison.Ordinal)..];
        int shuffleIndex = method.IndexOf("commandBar.PrimaryCommands.Add(shuffleButton);", StringComparison.Ordinal);
        int refreshIndex = method.IndexOf("commandBar.PrimaryCommands.Add(refreshButton);", StringComparison.Ordinal);

        Assert.True(shuffleIndex >= 0);
        Assert.True(refreshIndex > shuffleIndex);
        Assert.Contains("RefreshProfileMedia(profile)", method);
    }

    [Fact]
    public void StartPreviewLoad_UsesCacheStatusAndBackgroundRefresh()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Preview.cs"));
        string method = source[
            source.IndexOf("private async void StartPreviewLoad", StringComparison.Ordinal)..
            source.IndexOf("private void CancelPreviewLoad", StringComparison.Ordinal)];

        Assert.Contains("GetOrLoadOrderedImagesWithStatusAsync", method);
        Assert.Contains("profile.IncludeSubdirectories", method);
        Assert.Contains("result.LoadedFromCache", method);
        Assert.Contains("RefreshPreviewCacheAsync", method);
    }

    [Fact]
    public void BuildHardwareEditorPage_ContainsPreviewAndFormatSections()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private UIElement BuildHardwareEditorPage", StringComparison.Ordinal)..
            source.IndexOf("private Border CreateHardwareMonitorSettingsSection", StringComparison.Ordinal)];

        Assert.Contains("CreateHardwareEditorPreviewSection(config)", method);
        Assert.Contains("Width = GridLength.Auto", method);
        Assert.Contains("CreateHardwareEditorResizeHandle(root, editorColumn)", method);
        Assert.Contains("root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });", method);
        Assert.DoesNotContain("MinimumHardwareEditorSettingsWidth", method);
        Assert.Contains("CreateHardwareOverlayFormatSection(config)", method);
        Assert.Contains("CreateHardwareElementSettingsSection(config)", method);
        Assert.Contains("previewSection.HorizontalAlignment = HorizontalAlignment.Left;", method);
        Assert.Contains("globalSection.HorizontalAlignment = HorizontalAlignment.Stretch;", method);
        Assert.Contains("elementSection.HorizontalAlignment = HorizontalAlignment.Stretch;", method);
        Assert.Contains("MinWidth = 0", method);
        Assert.Contains("formatGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });", method);
        Assert.Contains("formatGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });", method);
        Assert.Contains("Grid.SetColumn(formatGrid, 2)", method);
        Assert.DoesNotContain("formatScrollViewer", method);
    }

    [Fact]
    public void HardwareEditorSizing_UsesCompactPreviewPane()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));

        Assert.Contains("MinimumHardwareEditorPaneWidth = 320", source);
        Assert.Contains("MaximumHardwareEditorPaneWidth = 940", source);
        Assert.Contains("HardwareEditorPreviewResizeHandleSize", source);
        Assert.Contains("HardwareEditorPreviewResizeMinWidth = 240", source);
        Assert.Contains("HardwareEditorPreviewResizeMaxWidth = 900", source);
        Assert.Contains("_hardwareEditorSelectedElementIds", source);
        Assert.DoesNotContain("MinimumHardwareEditorPaneWidth = 560", source);
        Assert.DoesNotContain("MaximumHardwareEditorPaneWidth = 520", source);
        Assert.DoesNotContain("MaximumHardwareEditorPaneWidth = 1100", source);
        Assert.DoesNotContain("_hardwareEditorPreviewWidth", source);
        Assert.DoesNotContain("_hardwareEditorPreviewHeight", source);
    }

    [Fact]
    public void EstimateWindowHeightForSettingsPage_UsesThreeHardwareMonitorRows()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Windowing.cs"));
        string method = source[
            source.IndexOf("private static int EstimateWindowHeightForSettingsPage", StringComparison.Ordinal)..
            source.IndexOf("private static int EstimateWindowHeightForHardwareEditorPage", StringComparison.Ordinal)];

        Assert.Contains("EstimateSettingsSectionHeight(true, 3)", method);
        Assert.DoesNotContain("EstimateSettingsSectionHeight(true, 4)", method);
    }

    [Fact]
    public void MeasuredWindowHeight_UsesIntrinsicContentInsteadOfStretchingRoot()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Windowing.cs"));
        string xaml = File.ReadAllText(Path.Combine(root, "MainWindow.xaml"));
        string configureMethod = source[
            source.IndexOf("private void ConfigureSettingsWindow", StringComparison.Ordinal)..
            source.IndexOf("private int CalculatePreferredWindowHeight", StringComparison.Ordinal)];
        string resizeMethod = source[
            source.IndexOf("private void ResizeToMeasuredContentHeight", StringComparison.Ordinal)..
            source.IndexOf("private int CalculateMeasuredWindowHeight", StringComparison.Ordinal)];
        string measuredMethod = source[
            source.IndexOf("private int CalculateMeasuredWindowHeight", StringComparison.Ordinal)..
            source.IndexOf("private void MoveAndResizeSettingsWindow", StringComparison.Ordinal)];

        Assert.Contains("PreferredSettingsWindowWidth = 1178", source);
        Assert.Contains("int width = GetPreferredSettingsWindowWidth(workArea);", configureMethod);
        Assert.Contains("AppWindow.Size.Width > 0 ? AppWindow.Size.Width : GetPreferredSettingsWindowWidth(workArea)", resizeMethod);
        Assert.Contains("Math.Ceiling(PreferredSettingsWindowWidth * GetWindowScale())", source);
        Assert.DoesNotContain("Math.Min(1540, workArea.Width)", source);
        Assert.Contains("double logicalHeight = MeasureSettingsContentHeight(targetWidth / scale);", measuredMethod);
        Assert.Contains("x:Name=\"ContentFrame\"", xaml);
        Assert.Contains("MonitorContent.Content is not FrameworkElement content", source);
        Assert.Contains("MonitorContent.ActualWidth > 0", source);
        Assert.Contains("ContentFrame.Padding.Top", source);
        Assert.Contains("MeasureIntrinsicContentHeight(content, contentWidth)", source);
        Assert.Contains("element is ScrollViewer scrollViewer && scrollViewer.Content is FrameworkElement scrollContent", source);
        Assert.DoesNotContain("Root.Measure(", measuredMethod);
        Assert.DoesNotContain("Math.Max(measuredHeight, estimatedHeight)", measuredMethod);
        Assert.Contains("return Math.Clamp(measuredHeight, Math.Min(minimumHeight, maximumHeight), maximumHeight);", measuredMethod);
    }

    [Fact]
    public void HardwareEditorPreviewSection_UsesTightPreviewPadding()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string sectionMethod = source[
            source.IndexOf("private FrameworkElement CreateHardwareEditorPreviewSection", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateHardwareEditorActions", StringComparison.Ordinal)];
        string surfaceMethod = source[
            source.IndexOf("private FrameworkElement CreateHardwareEditorPreviewSurface", StringComparison.Ordinal)..
            source.IndexOf("private Border CreateHardwareElementSettingsSection", StringComparison.Ordinal)];

        Assert.Contains("Spacing = 6", sectionMethod);
        Assert.Contains("Padding = new Thickness(6)", sectionMethod);
        Assert.Contains("_hardwareEditorPreviewHost = new ContentControl", sectionMethod);
        Assert.Contains("Content = CreateHardwareEditorPreviewSurface(config)", sectionMethod);
        Assert.Contains("stack.Children.Add(_hardwareEditorPreviewHost);", sectionMethod);
        Assert.Contains("Padding = new Thickness(0)", surfaceMethod);
        Assert.Contains("EnsureHardwareOverlaySize(config);", surfaceMethod);
        Assert.Contains("config.OverlayWidth", surfaceMethod);
        Assert.Contains("config.OverlayHeight", surfaceMethod);
        Assert.Contains("new ScrollViewer", surfaceMethod);
        Assert.Contains("Content = canvas", surfaceMethod);
        Assert.Contains("Width = layout.Width", surfaceMethod);
        Assert.Contains("Height = layout.Height", surfaceMethod);
        Assert.Contains("CreateHardwareEditorPreviewResizeHandle(config, surfaceGrid, previewViewport, canvas)", surfaceMethod);
        Assert.Contains("surfaceGrid.Children.Add(previewViewport);", surfaceMethod);
        Assert.Contains("surfaceGrid.Children.Add(resizeHandle);", surfaceMethod);
        Assert.Contains("Child = surfaceGrid", surfaceMethod);
        Assert.DoesNotContain("new Viewbox", surfaceMethod);
        Assert.DoesNotContain("Stretch = Stretch.Uniform,", surfaceMethod);
        Assert.DoesNotContain("new HardwareOverlayLayout(720, 420)", surfaceMethod);
        Assert.DoesNotContain("Padding = new Thickness(8)", sectionMethod);
        Assert.DoesNotContain("Padding = new Thickness(16)", sectionMethod);
    }

    [Fact]
    public void HardwareEditorPreviewResizeHandle_UpdatesPreviewBounds()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareEditorPreviewResizeHandle", StringComparison.Ordinal)..
            source.IndexOf("private Border CreateHardwareElementSettingsSection", StringComparison.Ordinal)];

        Assert.Contains("HorizontalAlignment = HorizontalAlignment.Right", method);
        Assert.Contains("VerticalAlignment = VerticalAlignment.Bottom", method);
        Assert.Contains("Width = HardwareEditorPreviewResizeHandleSize", method);
        Assert.Contains("Height = HardwareEditorPreviewResizeHandleSize", method);
        Assert.Contains("handle.CapturePointer(args.Pointer);", method);
        Assert.Contains("config.OverlayWidth =", method);
        Assert.Contains("config.OverlayHeight =", method);
        Assert.Contains("canvas.Width = config.OverlayWidth;", method);
        Assert.Contains("canvas.Height = config.OverlayHeight;", method);
        Assert.Contains("ScheduleApplySettings();", method);
        Assert.Contains("Math.Clamp(startWidth + delta.X", method);
        Assert.Contains("Math.Clamp(startHeight + delta.Y", method);
        Assert.DoesNotContain("MaxWidth = _hardwareEditorPreviewWidth", method);
        Assert.DoesNotContain("MaxHeight = _hardwareEditorPreviewHeight", method);
    }

    [Fact]
    public void HardwareEditorResizeHandle_UsesSharedColumnResizeHandle()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private static FrameworkElement CreateHardwareEditorResizeHandle", StringComparison.Ordinal)..
            source.IndexOf("private UIElement BuildMonitorSettings", StringComparison.Ordinal)];

        Assert.Contains("CreateColumnResizeHandle(root, editorColumn, MinimumHardwareEditorPaneWidth, MaximumHardwareEditorPaneWidth)", method);
        Assert.Contains("resizedColumn.Width = new GridLength(width);", method);
        Assert.Contains("handle.CapturePointer(args.Pointer);", method);
        Assert.Contains("handle.ReleasePointerCapture(args.Pointer);", method);
    }

    [Fact]
    public void HardwareEditorPreviewSurface_AddsDragGuides()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareEditorPreviewSurface", StringComparison.Ordinal)..
            source.IndexOf("private Border CreateHardwareElementSettingsSection", StringComparison.Ordinal)];

        Assert.Contains("CreateHardwareEditorGuideLine(isVertical: true", method);
        Assert.Contains("CreateHardwareEditorGuideLine(isVertical: false", method);
        Assert.Contains("CreateHardwareEditorSelectionRectangle()", method);
        Assert.Contains("AttachHardwareEditorMarqueeSelection(canvas, config, selectionRectangle)", method);
        Assert.Contains("AttachHardwareEditorKeyboardNudge(canvas, config, visualsById)", method);
        Assert.Contains("IsTabStop = true", method);
        Assert.Contains("visualsById[element.Id] = visual;", method);
        Assert.Contains("AttachHardwareEditorDrag(canvas, visual, element, config, visual", method);
        Assert.Contains("GetHardwareEditorVisualSize(visual, canvas.Width, canvas.Height)", method);
        Assert.Contains("canvas.Children.Add(verticalGuide);", method);
        Assert.Contains("canvas.Children.Add(horizontalGuide);", method);
        Assert.Contains("canvas.Children.Add(selectionRectangle);", method);
    }

    [Fact]
    public void HardwareEditorDrag_UsesSnapGuides()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private void AttachHardwareEditorDrag", StringComparison.Ordinal)..
            source.IndexOf("private static bool TryCreateSettingsBitmapImage", StringComparison.Ordinal)];

        Assert.Contains("ApplyHardwareEditorSnap", method);
        Assert.Contains("GetHardwareEditorVisualSize(visual", method);
        Assert.Contains("GetHardwareEditorDragElements(config, element)", method);
        Assert.Contains("startPositions", method);
        Assert.Contains("snapElements = dragElements.Count > 1", method);
        Assert.Contains("!dragElements.Contains(item)", method);
        Assert.Contains("foreach (HardwareOverlayElement dragElement in dragElements)", method);
        Assert.Contains("visualsById.TryGetValue(dragElement.Id", method);
        Assert.Contains("UpdateHardwareEditorGuides", method);
        Assert.Contains("HideHardwareEditorGuides", method);
        Assert.Contains("HardwareEditorSnapThreshold", source);
    }

    [Fact]
    public void HardwareEditorDrag_SelectsImmediatelyWithoutFullPageRender()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string windowSource = File.ReadAllText(Path.Combine(root, "MainWindow.xaml.cs"));
        string buildMethod = source[
            source.IndexOf("private UIElement BuildHardwareEditorPage", StringComparison.Ordinal)..
            source.IndexOf("private Border CreateHardwareMonitorSettingsSection", StringComparison.Ordinal)];
        string dragMethod = source[
            source.IndexOf("private void AttachHardwareEditorDrag", StringComparison.Ordinal)..
            source.IndexOf("private List<HardwareOverlayElement> GetHardwareEditorDragElements", StringComparison.Ordinal)];
        int selectMethodStart = source.IndexOf("private void SelectHardwareEditorElement", StringComparison.Ordinal);
        int updateVisualStart = source.IndexOf("private static void UpdateHardwareEditorSelectionVisual", StringComparison.Ordinal);

        Assert.Contains("private ContentControl? _hardwareElementSettingsHost;", windowSource);
        Assert.Contains("_hardwareElementSettingsHost = new ContentControl", buildMethod);
        Assert.Contains("_hardwareElementSettingsHost.Content = CreateHardwareElementSettingsSection(config);", buildMethod);
        Assert.Contains("visual.PointerPressed += (_, args) =>", dragMethod);
        Assert.Contains("SelectHardwareEditorElement(config, element.Id, visualsById);", dragMethod);
        Assert.True(selectMethodStart >= 0);
        Assert.True(updateVisualStart > selectMethodStart);
        string selectMethod = source[selectMethodStart..updateVisualStart];
        Assert.Contains("UpdateHardwareEditorSelectionVisual(entry.Value", selectMethod);
        Assert.Contains("DispatcherQueue.TryEnqueue", selectMethod);
        Assert.Contains("_hardwareElementSettingsHost.Content = CreateHardwareElementSettingsSection(config);", selectMethod);
        Assert.DoesNotContain("visual.Tapped", dragMethod);
        Assert.DoesNotContain("RenderTabs(_selectedMonitorId)", dragMethod);
        Assert.Contains("visual.CapturePointer(args.Pointer);", dragMethod);
    }

    [Fact]
    public void HardwareEditorMarqueeSelection_SelectsIntersectingElements()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private void AttachHardwareEditorMarqueeSelection", StringComparison.Ordinal)..
            source.IndexOf("private static Rect CreateHardwareSelectionRect", StringComparison.Ordinal)];

        Assert.Contains("canvas.PointerPressed", method);
        Assert.Contains("canvas.CapturePointer(args.Pointer);", method);
        Assert.Contains("UpdateHardwareSelectionRectangle(selectionRectangle", method);
        Assert.Contains("HardwareEditorLayoutService.SelectIntersectingElementIds(config.Elements", method);
        Assert.Contains("_hardwareEditorSelectedElementIds.Clear();", method);
        Assert.Contains("_hardwareEditorSelectedElementIds.Add(id);", method);
        Assert.Contains("config.SelectedElementId = selectedIds.FirstOrDefault() ?? string.Empty;", method);
        Assert.Contains("RefreshHardwareEditorPreview(config);", method);
    }

    [Fact]
    public void HardwareEditorKeyboardNudge_MovesSelectionWithoutSnap()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private void AttachHardwareEditorKeyboardNudge", StringComparison.Ordinal)..
            source.IndexOf("private bool MoveHardwareEditorSelection", StringComparison.Ordinal)];
        string moveMethod = source[
            source.IndexOf("private bool MoveHardwareEditorSelection", StringComparison.Ordinal)..
            source.IndexOf("private void AttachHardwareEditorDrag", StringComparison.Ordinal)];

        Assert.Contains("canvas.KeyDown +=", method);
        Assert.Contains("RequestHardwareEditorKeyboardFocus", source);
        Assert.Contains("FocusHardwareEditorKeyboardCanvasIfPending", source);
        Assert.Contains("canvas.Loaded += (_, _) => FocusHardwareEditorKeyboardCanvasIfPending(canvas);", source);
        Assert.Contains("_hardwareEditorKeyboardFocusPending = true;", source);
        Assert.Contains("global::Windows.System.VirtualKey.Left", method);
        Assert.Contains("MoveHardwareEditorSelection(config, visualsById, canvas.Width, canvas.Height", method);
        Assert.Contains("args.Handled = true;", method);
        Assert.Contains("GetHardwareEditorDragElements(config, selectedElement)", moveMethod);
        Assert.Contains("Canvas.SetLeft(visual", moveMethod);
        Assert.Contains("ScheduleApplySettings();", moveMethod);
        Assert.DoesNotContain("ApplyHardwareEditorSnap", method);
        Assert.DoesNotContain("ApplyHardwareEditorSnap", moveMethod);
    }

    [Fact]
    public void HardwareEditorMovement_QuantizesElementCoordinates()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string moveMethod = source[
            source.IndexOf("private bool MoveHardwareEditorSelection", StringComparison.Ordinal)..
            source.IndexOf("private void AttachHardwareEditorDrag", StringComparison.Ordinal)];
        string dragMethod = source[
            source.IndexOf("private void AttachHardwareEditorDrag", StringComparison.Ordinal)..
            source.IndexOf("private List<HardwareOverlayElement> GetHardwareEditorDragElements", StringComparison.Ordinal)];
        string positionMethod = source[
            source.IndexOf("private FrameworkElement CreateHardwareElementPositionControls", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateHardwareElementSizeControls", StringComparison.Ordinal)];

        Assert.Contains("HardwareEditorLayoutService.QuantizePosition", moveMethod);
        Assert.Contains("HardwareEditorLayoutService.QuantizePosition", dragMethod);
        Assert.Contains("HardwareEditorLayoutService.QuantizeCoordinate", positionMethod);
    }

    [Fact]
    public void HardwareEditorActions_AddsArrangeGridButton()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareEditorActions", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateHardwareEditorPreviewSurface", StringComparison.Ordinal)];

        Assert.Contains("HardwareMonitorArrangeGrid", method);
        Assert.Contains("ApplyHardwareEditorGridSpacing(config)", method);
        Assert.Contains("panel.Children.Add(arrangeGridButton);", method);
    }

    [Fact]
    public void HardwareEditorSnap_UsesOnlyElementEdges()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string guidesMethod = source[
            source.IndexOf("private static IEnumerable<double> GetHardwareEditorAxisGuides", StringComparison.Ordinal)..
            source.IndexOf("private static IEnumerable<double> GetHardwareEditorAxisOffsets", StringComparison.Ordinal)];
        string offsetsMethod = source[
            source.IndexOf("private static IEnumerable<double> GetHardwareEditorAxisOffsets", StringComparison.Ordinal)..
            source.IndexOf("private static void UpdateHardwareEditorGuides", StringComparison.Ordinal)];

        Assert.Contains("yield return position;", guidesMethod);
        Assert.Contains("yield return position + size;", guidesMethod);
        Assert.DoesNotContain("size / 2", guidesMethod);
        Assert.Contains("yield return 0;", offsetsMethod);
        Assert.Contains("yield return size;", offsetsMethod);
        Assert.DoesNotContain("size / 2", offsetsMethod);
    }

    [Fact]
    public void HardwareEditorElementVisual_UsesSharedOverlayFactory()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareEditorElementVisual", StringComparison.Ordinal)..
            source.IndexOf("private static (double Width, double Height) GetHardwareEditorVisualSize", StringComparison.Ordinal)];

        Assert.Contains("HardwareOverlayVisualFactory.CreateElement(element)", method);
        Assert.Contains("Width = element.Width", method);
        Assert.Contains("Height = element.Height", method);
        Assert.Contains("IsHitTestVisible = false", method);
        Assert.DoesNotContain("CreateHardwareEditorSensorVisual", source);
    }

    [Fact]
    public void CreateHardwareMonitorSettingsSection_ContainsRefreshIntervalRow()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private Border CreateHardwareMonitorSettingsSection", StringComparison.Ordinal)..
            source.IndexOf("private IReadOnlyList<Choice<string>> CreateHardwareMonitorTargetChoices", StringComparison.Ordinal)];

        Assert.Contains("HardwareMonitorRefreshInterval", method);
        Assert.Contains("config.RefreshIntervalSeconds", method);
        Assert.Contains("CreateHardwareMonitorEnabledCheckBox(config)", method);
        Assert.Contains("private void SetHardwareMonitorEnabled", method);
        Assert.Contains("ApplySettings();", method);
    }

    [Fact]
    public void CreateHardwareOverlayFormatSection_UsesFontCombo()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private Border CreateHardwareOverlayFormatSection", StringComparison.Ordinal)..
            source.IndexOf("private IReadOnlyList<Choice<string>> CreateHardwareMonitorTargetChoices", StringComparison.Ordinal)];

        Assert.Contains("CreateHardwareFontCombo(config.FontFamily", method);
        Assert.Contains("RenderTabs(_selectedMonitorId);", method);
        Assert.Contains("HardwareMonitorBackground", method);
        Assert.Contains("CreateHardwareBackgroundControls(config)", method);
        Assert.Contains("HardwareMonitorBackgroundColor", method);
        Assert.Contains("CreateHardwareColorPicker(GetHardwareBackgroundColor(config)", method);
        Assert.Contains("config.BackgroundColor = value;", method);
        Assert.Contains("CreateOpacitySlider(config.Opacity", method);
        Assert.DoesNotContain("CreateNumberBox(config.Opacity", method);
        Assert.DoesNotContain("HardwareMonitorTemplate", method);
        Assert.DoesNotContain("HardwareMonitorTemplateActions", method);
        Assert.DoesNotContain("CreateHardwareTextBox(config.FontFamily", method);
    }

    [Fact]
    public void CreateHardwareBackgroundControls_ExposesImageButtonsAndClearAction()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareBackgroundControls", StringComparison.Ordinal)..
            source.IndexOf("private async Task ShowHardwareSensorSelectionDialogAsync", StringComparison.Ordinal)];

        Assert.Contains("ImportHardwareBackgroundAsync(config)", method);
        Assert.Contains("AutomationProperties.SetName(backgroundButton", method);
        Assert.Contains("config.BackgroundImagePath = string.Empty;", method);
        Assert.Contains("config.BackgroundColor = string.Empty;", method);
        Assert.Contains("RenderTabs(_selectedMonitorId);", method);
    }

    [Fact]
    public void HardwareOverlayBackgroundColor_FlowsToEditorAndOverlayWindow()
    {
        string root = FindProjectRoot();
        string mainWindowSource = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string coordinatorSource = File.ReadAllText(Path.Combine(root, "Services", "WallpaperPlaybackCoordinator.cs"));
        string overlaySource = File.ReadAllText(Path.Combine(root, "Windows", "HardwareOverlayWindow.xaml.cs"));

        Assert.Contains("Background = HardwareOverlayVisualFactory.CreateBrush(GetHardwareBackgroundColor(config)", mainWindowSource);
        Assert.Contains("BackgroundColor = _hardwareMonitorConfig.BackgroundColor", coordinatorSource);
        Assert.Contains("HardwareOverlay.Background = HardwareOverlayVisualFactory.CreateBrush(state.BackgroundColor", overlaySource);
    }

    [Fact]
    public void CreateHardwareOverlayFormatSection_SyncsGlobalFontStyleToAllTextElements()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string formatMethod = source[
            source.IndexOf("private Border CreateHardwareOverlayFormatSection", StringComparison.Ordinal)..
            source.IndexOf("private IReadOnlyList<Choice<string>> CreateHardwareMonitorTargetChoices", StringComparison.Ordinal)];

        Assert.Contains("ApplyHardwareGlobalFontFamily(config, nextFontFamily);", formatMethod);
        Assert.Contains("ApplyHardwareGlobalFontSize(config, nextFontSize);", formatMethod);

        string familyMethod = source[
            source.IndexOf("private static void ApplyHardwareGlobalFontFamily", StringComparison.Ordinal)..
            source.IndexOf("private static void ApplyHardwareGlobalFontSize", StringComparison.Ordinal)];
        Assert.Contains("element.Kind != HardwareOverlayElementKind.Image", familyMethod);
        Assert.Contains("element.FontFamily = newFontFamily;", familyMethod);
        Assert.DoesNotContain("string.IsNullOrWhiteSpace(element.FontFamily)", familyMethod);
        Assert.DoesNotContain("string.Equals(element.FontFamily, oldFontFamily", familyMethod);

        string sizeMethod = source[
            source.IndexOf("private static void ApplyHardwareGlobalFontSize", StringComparison.Ordinal)..
            source.IndexOf("private IReadOnlyList<Choice<string>> CreateHardwareMonitorTargetChoices", StringComparison.Ordinal)];
        Assert.Contains("element.Kind != HardwareOverlayElementKind.Image", sizeMethod);
        Assert.Contains("element.FontSize = newFontSize;", sizeMethod);
        Assert.DoesNotContain("element.FontSize <= 0", sizeMethod);
        Assert.DoesNotContain("Math.Abs(element.FontSize - oldFontSize)", sizeMethod);
    }

    [Fact]
    public void CreateOpacitySlider_UsesPercentRangeForStoredOpacity()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.SettingsControls.cs"));
        string method = source[
            source.IndexOf("private Grid CreateOpacitySlider", StringComparison.Ordinal)..
            source.IndexOf("private Grid CreateTimedNumberBox", StringComparison.Ordinal)];

        Assert.Contains("Minimum = 10", method);
        Assert.Contains("Maximum = 100", method);
        Assert.Contains("Value = Math.Clamp(value, 0.1, 1) * 100", method);
        Assert.Contains("valueText.Text = $\"{percentage:0}%\";", method);
        Assert.Contains("changed(percentage / 100);", method);
        Assert.Contains("ScheduleApplySettings();", method);
    }

    [Fact]
    public void CreateHardwareEditorActions_ExcludesImageAndBackgroundButtons()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareEditorActions", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateHardwareEditorPreviewSurface", StringComparison.Ordinal)];

        Assert.Contains("HardwareMonitorAddSensorElements", method);
        Assert.Contains("HardwareMonitorAddTextElement", method);
        Assert.Contains("ShowHardwareSensorSelectionDialogAsync(config)", method);
        Assert.Contains("AddSelectedHardwareSensorsToEditor(config)", method);
        Assert.Contains("AutomationProperties.SetName(addSensorsButton", method);
        Assert.Contains("ImportHardwareTemplate", method);
        Assert.Contains("ExportHardwareTemplate", method);
        Assert.Contains("ImportHardwareTemplateAsync(config)", method);
        Assert.Contains("ExportHardwareTemplateAsync(config)", method);
        Assert.Contains("AutomationProperties.SetName(importTemplateButton", method);
        Assert.Contains("AutomationProperties.SetName(exportTemplateButton", method);
        Assert.DoesNotContain("HardwareMonitorImportIconImage", method);
        Assert.DoesNotContain("HardwareMonitorImportBackground", method);
        Assert.DoesNotContain("HardwareMonitorClearBackground", method);
    }

    [Fact]
    public void HardwareTemplateMethods_UseTemplateServiceAndFilePickers()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string importMethod = source[
            source.IndexOf("private async Task ImportHardwareTemplateAsync", StringComparison.Ordinal)..
            source.IndexOf("private async Task ExportHardwareTemplateAsync", StringComparison.Ordinal)];
        string exportMethod = source[
            source.IndexOf("private async Task ExportHardwareTemplateAsync", StringComparison.Ordinal)..
            source.IndexOf("private async Task ReplaceHardwareElementImageAsync", StringComparison.Ordinal)];

        Assert.Contains("PickOpenFileAsync(_hwnd, \".json\")", importMethod);
        Assert.Contains("HardwareOverlayTemplateService.ImportAsync(path)", importMethod);
        Assert.Contains("HardwareOverlayTemplateService.ApplyToConfig(template, config)", importMethod);
        Assert.Contains("ScheduleApplySettings();", importMethod);
        Assert.Contains("RenderTabs(_selectedMonitorId);", importMethod);
        Assert.Contains("PickSaveFileAsync(_hwnd, \".json\", \"hardware-overlay-template\")", exportMethod);
        Assert.Contains("HardwareOverlayTemplateService.FromConfig(config)", exportMethod);
        Assert.Contains("HardwareOverlayTemplateService.ExportAsync", exportMethod);
    }

    [Fact]
    public void CreateHardwareElementSettings_UsesFontComboAndColorPicker()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareElementSettings", StringComparison.Ordinal)..
            source.IndexOf("private string GetHardwareElementSensorDisplayName", StringComparison.Ordinal)];

        Assert.Contains("CreateHardwareFontCombo", method);
        Assert.Contains("CreateHardwareColorPicker", method);
        Assert.Contains("CreateHardwareSensorIconControls(config, element)", method);
        Assert.Contains("HardwareMonitorDecimalPlaces", method);
        Assert.Contains("element.DecimalPlaces = Math.Clamp((int)Math.Round(value), 0, 6);", method);
        Assert.Contains("GetHardwareElementDecimalPlaces(element)", method);
        Assert.Contains("CreateReplaceHardwareElementImageButton(config, element)", method);
        Assert.Contains("CreateOpacitySlider(element.Opacity", method);
        Assert.Contains("element.Foreground = value;", method);
        Assert.Contains("RefreshHardwareEditorPreview(config);", method);
        Assert.DoesNotContain("CreateNumberBox(element.Opacity", method);
        Assert.DoesNotContain("CreateHardwareTextBox(element.Foreground", method);
    }

    [Fact]
    public void RefreshHardwareEditorPreview_ReplacesOnlyPreviewHostContent()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private void RefreshHardwareEditorPreview", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateHardwareEditorActions", StringComparison.Ordinal)];

        Assert.Contains("_hardwareEditorPreviewHost.Content = CreateHardwareEditorPreviewSurface(config);", method);
        Assert.DoesNotContain("RenderTabs(_selectedMonitorId)", method);
    }

    [Fact]
    public void CreateHardwareFontCombo_LoadsFontsBeforeReopeningDropDown()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareFontCombo", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateHardwareColorPicker", StringComparison.Ordinal)];

        Assert.Contains("combo.IsDropDownOpen = false;", method);
        Assert.Contains("reopenDropDownAfterLoad", method);
        Assert.Contains("combo.IsDropDownOpen = true;", method);
        Assert.Contains("suppressSelectionChanged", method);
    }

    [Fact]
    public void CreateHardwareSensorIconControls_ClearsCustomImagePath()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareSensorIconControls", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateReplaceHardwareElementImageButton", StringComparison.Ordinal)];

        Assert.Contains("CreateReplaceHardwareElementImageButton(config, element)", method);
        Assert.Contains("HardwareMonitorResetIconImage", method);
        Assert.Contains("element.ImagePath = string.Empty;", method);
        Assert.Contains("config.SelectedElementId = element.Id;", method);
    }

    [Fact]
    public void HardwareOverlayVisualFactory_SensorElementUsesCustomIconBeforeDefaultIcon()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "HardwareOverlayVisualFactory.cs"));
        string method = source[
            source.IndexOf("private static FrameworkElement CreateSensorElement", StringComparison.Ordinal)..
            source.IndexOf("public static bool TryCreateBitmapImage", StringComparison.Ordinal)];

        Assert.Contains("TryCreateBitmapImage(element.ImagePath", method);
        Assert.Contains("new Image", method);
        Assert.Contains("Stretch = Stretch.Uniform", method);
        Assert.Contains("HardwareOverlayIconFactory.CreateIcon(element.IconKind, iconSize, brush)", method);
    }

    [Fact]
    public void HardwareOverlayIconFactory_UsesBladeDiskAndMotherboardShapes()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Windows", "HardwareOverlayIconFactory.cs"));
        string fanMethod = source[
            source.IndexOf("private static void DrawFan", StringComparison.Ordinal)..
            source.IndexOf("private static void DrawMemory", StringComparison.Ordinal)];
        string storageMethod = source[
            source.IndexOf("private static void DrawStorage", StringComparison.Ordinal)..
            source.IndexOf("private static void DrawThermometer", StringComparison.Ordinal)];
        string motherboardMethod = source[
            source.IndexOf("private static void DrawMotherboard", StringComparison.Ordinal)..
            source.IndexOf("private static void DrawThermometer", StringComparison.Ordinal)];

        Assert.Contains("case HardwareOverlayIconKind.Motherboard:", source);
        Assert.Contains("DrawMotherboard(canvas, brush, scale);", source);
        Assert.Contains("AddFanBlade", fanMethod);
        Assert.Contains("RotateTransform", source);
        Assert.DoesNotContain("AddEllipse(canvas, 8.5, 2.5, 3, 7", fanMethod);
        Assert.Contains("AddEllipse(canvas, 6, 6, 8, 8", storageMethod);
        Assert.Contains("AddLine(canvas, 10, 10, 14, 7", storageMethod);
        Assert.Contains("AddCircuitNode", motherboardMethod);
        Assert.Contains("AddLine(canvas, 5, 7, 9, 7", motherboardMethod);
    }

    [Fact]
    public void ReplaceHardwareElementImage_UpdatesSelectedElementWithoutAddingNewElement()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private async Task ReplaceHardwareElementImageAsync", StringComparison.Ordinal)..
            source.IndexOf("private async Task ImportHardwareBackgroundAsync", StringComparison.Ordinal)];

        Assert.Contains("element.ImagePath = path;", method);
        Assert.Contains("config.SelectedElementId = element.Id;", method);
        Assert.DoesNotContain("CreateDefaultHardwareElement", method);
        Assert.DoesNotContain("config.Elements.Add", method);
    }

    [Fact]
    public void CreateHardwareMonitorSettingsSection_DoesNotShowSensorDialogButton()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private Border CreateHardwareMonitorSettingsSection", StringComparison.Ordinal)..
            source.IndexOf("private IReadOnlyList<Choice<string>> CreateHardwareMonitorTargetChoices", StringComparison.Ordinal)];

        Assert.Contains("HardwareMonitorEnabled", method);
        Assert.Contains("HardwareMonitorRefreshInterval", method);
        Assert.Contains("HardwareMonitorTargetDisplay", method);
        Assert.DoesNotContain("CreateHardwareSensorDialogButton(config)", method);
        Assert.DoesNotContain("HardwareMonitorSensors", method);
        Assert.DoesNotContain("CreateHardwareSensorSelectionList(config)", method);
        Assert.DoesNotContain("IsFullWidth: true", method);
        Assert.DoesNotContain("private FrameworkElement CreateHardwareSensorDialogButton", source);
    }

    [Fact]
    public void ShowHardwareSensorSelectionDialog_UsesContentDialog()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private async Task ShowHardwareSensorSelectionDialogAsync", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateHardwareSensorSelectionList", StringComparison.Ordinal)];

        Assert.Contains("new ContentDialog", method);
        Assert.Contains("XamlRoot = Root.XamlRoot", method);
        Assert.Contains("CreateHardwareSensorSelectionList(config, ReloadDialogSensors)", method);
        Assert.Contains("CloseButtonText = LocalizedStrings.Get(\"DialogClose\")", method);
    }

    [Fact]
    public void CreateSettingsRow_SupportsFullWidthRows()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.SettingsControls.cs"));
        string method = source[
            source.IndexOf("private static Border CreateSettingsRow", StringComparison.Ordinal)..
            source.IndexOf("private static Border CreateSettingsDivider", StringComparison.Ordinal)];

        Assert.Contains("row.IsFullWidth", method);
        Assert.Contains("AddSettingsRowControl(content, row.Control, column: 0)", method);
        Assert.Contains("AutomationProperties.SetName(text, row.Label);", method);
    }

    [Fact]
    public void CreateSettingsContentSection_StretchesAndScrollsContent()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.SettingsControls.cs"));
        string method = source[
            source.IndexOf("private static Border CreateSettingsContentSection", StringComparison.Ordinal)..
            source.IndexOf("private static Border CreateSettingsRow", StringComparison.Ordinal)];

        Assert.Contains("new GridLength(1, GridUnitType.Star)", method);
        Assert.Contains("new ScrollViewer", method);
        Assert.Contains("VerticalAlignment = VerticalAlignment.Stretch", method);
        Assert.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Auto", method);
    }

    [Fact]
    public void CreateHardwareSensorSelectionList_ShowsBrokerElevationNotice()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareSensorSelectionList", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateHardwarePositionControls", StringComparison.Ordinal)];

        Assert.Contains("snapshot is not { IsElevated: true }", method);
        Assert.Contains("CreateHardwareBrokerElevationNotice(refreshRequested)", method);
        Assert.Contains("HardwareMonitorRestartBrokerAsAdministrator", method);
        Assert.DoesNotContain("CurrentProcessPrivilege.IsAdministrator", method);
    }

    [Fact]
    public void CreateHardwareSensorSelectionContent_UsesIconAndNameOnly()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private static FrameworkElement CreateHardwareSensorSelectionContent", StringComparison.Ordinal)..
            source.IndexOf("private static HardwareOverlayIconKind GetHardwareMetricKindIcon", StringComparison.Ordinal)];

        Assert.Contains("ColumnSpacing = 0", method);
        Assert.Contains("GetHardwareMetricKindIcon(sensor.Kind)", method);
        Assert.Contains("Text = $\" {GetHardwareMetricGroupLabel(sensor.Group)} - {sensor.DisplayName}\"", method);
        Assert.DoesNotContain("GetHardwareMetricKindLabel", method);
        Assert.DoesNotContain("MinWidth = 58", method);
    }

    [Fact]
    public void HardwareMonitorReader_AvoidsControllerAndPsuCollectors()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorReader.cs"));
        string method = source[
            source.IndexOf("private Computer EnsureComputer", StringComparison.Ordinal)..
            source.IndexOf("private static void CollectHardware", StringComparison.Ordinal)];

        Assert.Contains("IsCpuEnabled = profile.Cpu", method);
        Assert.Contains("IsGpuEnabled = profile.Gpu", method);
        Assert.Contains("IsMemoryEnabled = profile.Memory", method);
        Assert.Contains("IsMotherboardEnabled = profile.Motherboard", method);
        Assert.Contains("IsStorageEnabled = profile.Storage", method);
        Assert.DoesNotContain("IsControllerEnabled = true", method);
        Assert.DoesNotContain("IsPsuEnabled = true", method);
    }

    [Fact]
    public void HardwareMonitorService_ReadsHardwareThroughBroker()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorService.cs"));
        string appSource = File.ReadAllText(Path.Combine(root, "App.xaml.cs"));
        string clientSource = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorBrokerClient.cs"));

        Assert.Contains("HardwareMonitorBrokerClient", source);
        Assert.Contains("_brokerClient.StartBroker()", source);
        Assert.Contains("_brokerClient.StopBroker()", source);
        Assert.Contains("_brokerClient.GetSnapshot(config)", source);
        Assert.Contains("public bool StartBroker()", clientSource);
        Assert.Contains("public void StopBroker()", clientSource);
        Assert.Contains("public void SetBrokerElevation(bool startElevated)", source);
        Assert.Contains("public void SetStartElevated(bool startElevated)", clientSource);
        Assert.Contains("startInfo.Verb = \"runas\";", clientSource);
        Assert.Contains("TryGetSnapshot(sensorIds, restartBroker: true)", clientSource);
        Assert.DoesNotContain("LibreHardwareMonitor", source);
        Assert.DoesNotContain("new Computer", source);
        Assert.DoesNotContain("new HardwareMonitorReader", appSource);
    }

    [Fact]
    public void AppLaunch_WithQuietStart_ActivatesThenHidesWindow()
    {
        string root = FindProjectRoot();
        string appSource = File.ReadAllText(Path.Combine(root, "App.xaml.cs"));
        string windowingSource = File.ReadAllText(Path.Combine(root, "MainWindow.Windowing.cs"));

        Assert.Contains("launchOptions.StartInTray && _window is MainWindow mainWindow", appSource);
        Assert.Contains("mainWindow.ActivateHiddenTrayStartupWindow();", appSource);
        Assert.Contains("public void ActivateHiddenTrayStartupWindow()", windowingSource);
        Assert.Contains("Activate();", windowingSource);
        Assert.Contains("NativeMethods.ShowWindow(_hwnd, NativeMethods.SW_HIDE);", windowingSource);
    }

    [Fact]
    public void HardwareMonitorBroker_IsSingleFileEmbeddedWithSeparateTitle()
    {
        string root = FindProjectRoot();
        string project = File.ReadAllText(Path.Combine(root, "SlideShowWallpaper.csproj"));
        string brokerProject = File.ReadAllText(Path.Combine(root, "HardwareBroker", "SlideShowWallpaper.HardwareBroker.csproj"));
        string brokerProgram = File.ReadAllText(Path.Combine(root, "HardwareBroker", "Program.cs"));

        Assert.Contains("PublishSingleFile=true", project);
        Assert.Contains("<EmbeddedResource Include=\"$(HardwareBrokerEmbeddedExecutable)\"", project);
        Assert.Contains("<AssemblyName>SlideShowWallpaper.HardwareBroker</AssemblyName>", brokerProject);
        Assert.Contains("<AssemblyTitle>SlideShowWallpaper Broker</AssemblyTitle>", brokerProject);
        Assert.Contains("<FileDescription>SlideShowWallpaper Broker</FileDescription>", brokerProject);
        Assert.Contains("<Product>SlideShowWallpaper Broker</Product>", brokerProject);
        Assert.Contains("Console.Title = \"SlideShowWallpaper Broker\";", brokerProgram);
    }

    [Fact]
    public void RenderTabs_AddsHardwareEditorNavigationAboveSettings()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Navigation.cs"));
        string method = source[
            source.IndexOf("private void RenderTabs", StringComparison.Ordinal)..
            source.IndexOf("private Button CreateMonitorNavigationItem", StringComparison.Ordinal)];
        int editorIndex = method.IndexOf("SettingsNavigationPanel.Children.Add(CreateHardwareEditorNavigationItem());", StringComparison.Ordinal);
        int settingsIndex = method.IndexOf("SettingsNavigationPanel.Children.Add(CreateSettingsNavigationItem());", StringComparison.Ordinal);

        Assert.DoesNotContain("CreateHardwareMonitorNavigationItem", method);
        Assert.True(editorIndex >= 0);
        Assert.True(settingsIndex > editorIndex);
    }

    [Fact]
    public void ShowSelectedMonitorPage_WhenHardwareEditorSelected_RendersEditorPage()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Navigation.cs"));
        string method = source[
            source.IndexOf("private void ShowSelectedMonitorPage", StringComparison.Ordinal)..
            source.IndexOf("private void MonitorNavigationItem_Click", StringComparison.Ordinal)];

        Assert.DoesNotContain("_isHardwareMonitorSelected", method);
        Assert.DoesNotContain("BuildHardwareMonitorSettingsPage()", method);
        Assert.Contains("if (_isHardwareEditorSelected)", method);
        Assert.Contains("BuildHardwareEditorPage()", method);
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
