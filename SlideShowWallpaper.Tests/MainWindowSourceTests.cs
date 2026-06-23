namespace SlideShowWallpaper.Tests;

public sealed class MainWindowSourceTests
{
    [Fact]
    public void Constructor_WithStartInTray_TrimsBackgroundMemoryAfterApplyingSettings()
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
        Assert.Contains("TrimBackgroundMemory();", constructor[trimIndex..]);
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
