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
    public void BuildHardwareEditorPage_ContainsPreviewAndFormatSections()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private UIElement BuildHardwareEditorPage", StringComparison.Ordinal)..
            source.IndexOf("private Border CreateHardwareMonitorSettingsSection", StringComparison.Ordinal)];

        Assert.Contains("CreateHardwareEditorPreviewSection(config)", method);
        Assert.Contains("new GridLength(DefaultHardwareEditorPaneWidth)", method);
        Assert.Contains("CreateHardwareEditorResizeHandle(root, editorColumn)", method);
        Assert.Contains("MinimumHardwareEditorSettingsWidth", method);
        Assert.Contains("CreateHardwareOverlayFormatSection(config)", method);
        Assert.Contains("CreateHardwareElementSettingsSection(config)", method);
        Assert.Contains("formatGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });", method);
        Assert.Contains("formatGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });", method);
        Assert.Contains("Grid.SetColumn(formatGrid, 2)", method);
        Assert.DoesNotContain("formatScrollViewer", method);
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
        Assert.Contains("AttachHardwareEditorDrag(canvas, visual, element, config, verticalGuide, horizontalGuide)", method);
        Assert.Contains("GetHardwareEditorVisualSize(visual, canvas.Width, canvas.Height)", method);
        Assert.Contains("canvas.Children.Add(verticalGuide);", method);
        Assert.Contains("canvas.Children.Add(horizontalGuide);", method);
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
        Assert.Contains("UpdateHardwareEditorGuides", method);
        Assert.Contains("HideHardwareEditorGuides", method);
        Assert.Contains("HardwareEditorSnapThreshold", source);
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
    public void HardwareEditorElementVisual_UsesAutoSizeForTextElements()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private FrameworkElement CreateHardwareEditorElementVisual", StringComparison.Ordinal)..
            source.IndexOf("private static FrameworkElement CreateHardwareEditorSensorVisual", StringComparison.Ordinal)];

        Assert.Contains("if (element.Kind == HardwareOverlayElementKind.Image)", method);
        Assert.Contains("host.Width = element.Width;", method);
        Assert.Contains("host.Height = element.Height;", method);
        Assert.DoesNotContain("Width = element.Width,", method);
        Assert.DoesNotContain("Height = element.Height,", method);
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
    public void CreateHardwareOverlayFormatSection_UsesFontCombo()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private Border CreateHardwareOverlayFormatSection", StringComparison.Ordinal)..
            source.IndexOf("private IReadOnlyList<Choice<string>> CreateHardwareMonitorTargetChoices", StringComparison.Ordinal)];

        Assert.Contains("CreateHardwareFontCombo(config.FontFamily", method);
        Assert.Contains("HardwareMonitorBackground", method);
        Assert.Contains("CreateHardwareBackgroundControls(config)", method);
        Assert.DoesNotContain("CreateHardwareTextBox(config.FontFamily", method);
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
        Assert.DoesNotContain("HardwareMonitorImportIconImage", method);
        Assert.DoesNotContain("HardwareMonitorImportBackground", method);
        Assert.DoesNotContain("HardwareMonitorClearBackground", method);
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
        Assert.Contains("CreateReplaceHardwareElementImageButton(config, element)", method);
        Assert.DoesNotContain("CreateHardwareTextBox(element.Foreground", method);
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
    public void CreateHardwareMonitorSettingsSection_UsesSensorDialogButton()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "MainWindow.Settings.cs"));
        string method = source[
            source.IndexOf("private Border CreateHardwareMonitorSettingsSection", StringComparison.Ordinal)..
            source.IndexOf("private IReadOnlyList<Choice<string>> CreateHardwareMonitorTargetChoices", StringComparison.Ordinal)];

        Assert.Contains("CreateHardwareSensorDialogButton(config)", method);
        Assert.DoesNotContain("CreateHardwareSensorSelectionList(config)", method);
        Assert.DoesNotContain("IsFullWidth: true", method);
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
