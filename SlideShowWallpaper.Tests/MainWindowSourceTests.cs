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

        Assert.Contains("_viewModel.HardwareMonitor.X = Math.Max(0, args.X);", method);
        Assert.Contains("_viewModel.HardwareMonitor.Y = Math.Max(0, args.Y);", method);
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
    public void BuildAppSettingsPage_WrapsContentInScrollViewer()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private UIElement BuildAppSettingsPage", StringComparison.Ordinal)..
            source.IndexOf("private UIElement BuildHardwareMonitorSettingsPage", StringComparison.Ordinal)];

        Assert.Contains("new ScrollViewer", method);
        Assert.Contains("VerticalScrollBarVisibility = ScrollBarVisibility.Auto", method);
        Assert.Contains("HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled", method);
    }

    [Fact]
    public void BuildAppSettingsPage_DoesNotContainHardwareMonitorSection()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private UIElement BuildAppSettingsPage", StringComparison.Ordinal)..
            source.IndexOf("private UIElement BuildHardwareMonitorSettingsPage", StringComparison.Ordinal)];

        Assert.DoesNotContain("CreateHardwareMonitorSettingsSection()", method);
    }

    [Fact]
    public void BuildHardwareMonitorSettingsPage_ContainsHardwareMonitorSection()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private UIElement BuildHardwareMonitorSettingsPage", StringComparison.Ordinal)..
            source.IndexOf("private UIElement BuildHardwareEditorPage", StringComparison.Ordinal)];

        Assert.Contains("CreateHardwareMonitorSettingsSection()", method);
        Assert.Contains("new ScrollViewer", method);
        Assert.DoesNotContain("CreateHardwareEditorPreviewSection", method);
        Assert.DoesNotContain("CreateHardwareOverlayFormatSection", method);
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
        Assert.Contains("CreateHardwareOverlayFormatSection(config)", method);
        Assert.Contains("CreateHardwareElementSettingsSection(config)", method);
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
    }

    [Fact]
    public void CreateHardwareSensorSelectionList_ShowsLimitedAccessNotice()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareSensorSelectionList", StringComparison.Ordinal)..
            source.IndexOf("private FrameworkElement CreateHardwarePositionControls", StringComparison.Ordinal)];

        Assert.Contains("IsElevated: false", method);
        Assert.Contains("CreateHardwareSensorNotice()", method);
        Assert.Contains("HardwareMonitorRestartAsAdministrator", method);
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
    public void HardwareMonitorService_EnablesControllerAndPsuCollectors()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "HardwareMonitorService.cs"));
        string method = source[
            source.IndexOf("private Computer EnsureComputer", StringComparison.Ordinal)..
            source.IndexOf("private static void CollectHardware", StringComparison.Ordinal)];

        Assert.Contains("IsControllerEnabled = true", method);
        Assert.Contains("IsPsuEnabled = true", method);
    }

    [Fact]
    public void RenderTabs_AddsHardwareNavigationAboveSettings()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Navigation.cs"));
        string method = source[
            source.IndexOf("private void RenderTabs", StringComparison.Ordinal)..
            source.IndexOf("private Button CreateMonitorNavigationItem", StringComparison.Ordinal)];
        int hardwareIndex = method.IndexOf("SettingsNavigationPanel.Children.Add(CreateHardwareMonitorNavigationItem());", StringComparison.Ordinal);
        int editorIndex = method.IndexOf("SettingsNavigationPanel.Children.Add(CreateHardwareEditorNavigationItem());", StringComparison.Ordinal);
        int settingsIndex = method.IndexOf("SettingsNavigationPanel.Children.Add(CreateSettingsNavigationItem());", StringComparison.Ordinal);

        Assert.True(hardwareIndex >= 0);
        Assert.True(editorIndex > hardwareIndex);
        Assert.True(settingsIndex > editorIndex);
    }

    [Fact]
    public void ShowSelectedMonitorPage_WhenHardwarePagesSelected_RendersMatchingPage()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Navigation.cs"));
        string method = source[
            source.IndexOf("private void ShowSelectedMonitorPage", StringComparison.Ordinal)..
            source.IndexOf("private void MonitorNavigationItem_Click", StringComparison.Ordinal)];

        Assert.Contains("if (_isHardwareMonitorSelected)", method);
        Assert.Contains("BuildHardwareMonitorSettingsPage()", method);
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
