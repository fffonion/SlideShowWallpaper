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
            source.IndexOf("private Border CreateHardwareMonitorSettingsSection", StringComparison.Ordinal)];

        Assert.Contains("CreateHardwareMonitorSettingsSection()", method);
        Assert.Contains("new ScrollViewer", method);
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
    public void RenderTabs_AddsHardwareMonitorNavigationAboveSettings()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Navigation.cs"));
        string method = source[
            source.IndexOf("private void RenderTabs", StringComparison.Ordinal)..
            source.IndexOf("private Button CreateMonitorNavigationItem", StringComparison.Ordinal)];
        int hardwareIndex = method.IndexOf("SettingsNavigationPanel.Children.Add(CreateHardwareMonitorNavigationItem());", StringComparison.Ordinal);
        int settingsIndex = method.IndexOf("SettingsNavigationPanel.Children.Add(CreateSettingsNavigationItem());", StringComparison.Ordinal);

        Assert.True(hardwareIndex >= 0);
        Assert.True(settingsIndex > hardwareIndex);
    }

    [Fact]
    public void ShowSelectedMonitorPage_WhenHardwareMonitorSelected_RendersHardwarePage()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Navigation.cs"));
        string method = source[
            source.IndexOf("private void ShowSelectedMonitorPage", StringComparison.Ordinal)..
            source.IndexOf("private void MonitorNavigationItem_Click", StringComparison.Ordinal)];

        Assert.Contains("if (_isHardwareMonitorSelected)", method);
        Assert.Contains("BuildHardwareMonitorSettingsPage()", method);
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
