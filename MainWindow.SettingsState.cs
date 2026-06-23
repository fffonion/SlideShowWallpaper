using Microsoft.UI.Xaml;
using SlideShowWallpaper.Models;
using SlideShowWallpaper.Services;

namespace SlideShowWallpaper;

public sealed partial class MainWindow
{
    private async Task OpenFolderAsync(MonitorProfile profile)
    {
        string? folder = await _folderPickerService.PickFolderAsync(_hwnd);
        if (folder is null)
        {
            return;
        }

        profile.FolderPath = folder;
        profile.SelectedImagePath = string.Empty;
        RenderTabs(profile.Id);
        ApplySettings();
    }

    private void ApplySettings()
    {
        _settingsApplyTimer.Stop();
        WallpaperConfig config = CreateConfig();
        _settingsStore.Save(config);
        _coordinator.ApplyProfiles(
            config.Monitors,
            config.PlaybackEnabled,
            config.AutoTrackNewFiles,
            config.GlobalMute,
            config.PauseVideoWhenDisplayOffOrSleeping);
        UpdatePreviewPopupMute();
    }

    private void ScheduleApplySettings()
    {
        _settingsApplyTimer.Stop();
        _settingsApplyTimer.Start();
    }

    private WallpaperConfig CreateConfig()
    {
        WallpaperConfig existingConfig = _settingsStore.Load();
        return new WallpaperConfig
        {
            StartWithWindows = _viewModel.StartWithWindows,
            CloseToTray = _disableCloseToTray ? existingConfig.CloseToTray : _viewModel.CloseToTray,
            ThemeMode = _viewModel.ThemeMode,
            LanguageMode = _viewModel.LanguageMode,
            PlaybackEnabled = true,
            AutoTrackNewFiles = _viewModel.AutoTrackNewFiles,
            GlobalMute = _viewModel.GlobalMute,
            ThumbnailCacheEnabled = _viewModel.ThumbnailCacheEnabled,
            PauseVideoWhenDisplayOffOrSleeping = _viewModel.PauseVideoWhenDisplayOffOrSleeping,
            PreviewPopupDelaySeconds = Math.Max(PreviewPopupPolicy.MinimumHoverDelaySeconds, _viewModel.PreviewPopupDelaySeconds),
            Monitors = _viewModel.Profiles.ToList(),
        };
    }

    private void SetTheme(AppThemeMode themeMode)
    {
        if (_viewModel.ThemeMode == themeMode)
        {
            return;
        }

        _viewModel.ThemeMode = themeMode;
        ApplyTheme(themeMode);
        ApplySettings();
    }

    private void SetLanguage(AppLanguageMode languageMode)
    {
        if (_viewModel.LanguageMode == languageMode)
        {
            return;
        }

        _viewModel.LanguageMode = languageMode;
        AppLanguageService.Apply(languageMode);
        LocalizedStrings.Reset();
        Title = LocalizedStrings.Get("AppTitle");
        AppTitleBar.Title = LocalizedStrings.Get("AppTitle");
        RenderTabs(_selectedMonitorId);
        ApplySettings();
    }

    private void SetThumbnailCacheEnabled(bool isEnabled)
    {
        if (_viewModel.ThumbnailCacheEnabled == isEnabled)
        {
            return;
        }

        _viewModel.ThumbnailCacheEnabled = isEnabled;
        UnloadPreviewState();
        RenderTabs(_selectedMonitorId);
    }

    private async void StartThumbnailCacheSizeLoad()
    {
        int version = ++_thumbnailCacheSizeLoadVersion;
        _thumbnailCacheSizeCancellation?.Cancel();
        _thumbnailCacheSizeCancellation?.Dispose();
        _thumbnailCacheSizeCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _thumbnailCacheSizeCancellation.Token;

        ShowThumbnailCacheSizeLoading();
        try
        {
            long sizeBytes = await _thumbnailCacheService.GetCacheSizeBytesAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested || version != _thumbnailCacheSizeLoadVersion)
            {
                return;
            }

            ShowThumbnailCacheSize(FormatFileSize(sizeBytes));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            ShowThumbnailCacheSize(FormatFileSize(0));
        }
    }

    private async Task ClearThumbnailCacheAsync()
    {
        _thumbnailCacheSizeLoadVersion++;
        _thumbnailCacheSizeCancellation?.Cancel();
        _clearThumbnailCacheButton?.Focus(FocusState.Programmatic);
        SetThumbnailCacheControlsEnabled(false);
        ShowThumbnailCacheSizeLoading();
        try
        {
            await _thumbnailCacheService.ClearCacheAsync();
            ShowThumbnailCacheSize(FormatFileSize(0));
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            StartThumbnailCacheSizeLoad();
        }
        finally
        {
            SetThumbnailCacheControlsEnabled(true);
        }
    }

    private void ShowThumbnailCacheSizeLoading()
    {
        if (_thumbnailCacheSizeProgress is not null)
        {
            _thumbnailCacheSizeProgress.IsActive = true;
            _thumbnailCacheSizeProgress.Visibility = Visibility.Visible;
        }

        if (_thumbnailCacheSizeText is not null)
        {
            _thumbnailCacheSizeText.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowThumbnailCacheSize(string sizeText)
    {
        if (_thumbnailCacheSizeProgress is not null)
        {
            _thumbnailCacheSizeProgress.IsActive = false;
            _thumbnailCacheSizeProgress.Visibility = Visibility.Collapsed;
        }

        if (_thumbnailCacheSizeText is not null)
        {
            _thumbnailCacheSizeText.Text = sizeText;
            _thumbnailCacheSizeText.Visibility = Visibility.Visible;
        }
    }

    private void SetThumbnailCacheControlsEnabled(bool isEnabled)
    {
        if (_clearThumbnailCacheButton is not null)
        {
            _clearThumbnailCacheButton.IsEnabled = isEnabled;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.##} {units[unit]}";
    }

    private Func<ImageMetadata, CancellationToken, Task<string>> CreateThumbnailLoader()
    {
        return _viewModel.ThumbnailCacheEnabled
            ? _thumbnailCacheService.GetOrCreateThumbnailAsync
            : _thumbnailCacheService.CreateTemporaryThumbnailAsync;
    }

    private void ApplyTheme(AppThemeMode themeMode)
    {
        Root.RequestedTheme = themeMode switch
        {
            AppThemeMode.Light => ElementTheme.Light,
            AppThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    private static Choice<T> FindChoice<T>(IReadOnlyList<Choice<T>> choices, T selected)
        where T : notnull
    {
        return choices.FirstOrDefault(choice => EqualityComparer<T>.Default.Equals(choice.Value, selected)) ?? choices[0];
    }

    private void UnloadSettingsUiForTray()
    {
        UnloadSettingsUiForBackground();
    }

    private void UnloadSettingsUiForBackground()
    {
        if (_settingsUiUnloadedForBackground)
        {
            return;
        }

        _previewSessionVersion++;
        foreach (string monitorId in _previewLoadTokens.Keys.ToArray())
        {
            CancelPreviewLoad(monitorId);
        }

        MonitorNavigationPanel.Children.Clear();
        SettingsNavigationPanel.Children.Clear();
        MonitorContent.Content = null;
        UnloadPreviewState();
        _thumbnailCacheSizeCancellation?.Cancel();
        _thumbnailCacheSizeCancellation?.Dispose();
        _thumbnailCacheSizeCancellation = null;
        _thumbnailCacheSizeText = null;
        _thumbnailCacheSizeProgress = null;
        _clearThumbnailCacheButton = null;
        _settingsUiUnloadedForBackground = true;
        TrimBackgroundMemory();
    }

    private void EnsureSettingsUiLoaded()
    {
        if (!_settingsUiUnloadedForBackground && MonitorNavigationPanel.Children.Count > 0)
        {
            return;
        }

        _settingsUiUnloadedForBackground = false;
        RenderTabs(_selectedMonitorId);
    }

    private static void TrimBackgroundMemory()
    {
        ProcessMemoryTrimmer.TrimCurrentProcess();
    }
}
