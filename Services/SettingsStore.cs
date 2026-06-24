using System.Globalization;
using System.Text;
using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed class SettingsStore
{
    private const string SettingsFileName = "SlideShowWallpaper.ini";
    private readonly string _settingsPath;

    public SettingsStore()
        : this(GetDefaultSettingsPath())
    {
    }

    public SettingsStore(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public string SettingsPath => _settingsPath;

    public WallpaperConfig Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new WallpaperConfig();
        }

        try
        {
            Dictionary<string, Dictionary<string, string>> sections = Parse(File.ReadAllLines(_settingsPath));
            var config = new WallpaperConfig
            {
                StartWithWindows = GetBool(sections, "Settings", nameof(WallpaperConfig.StartWithWindows), false),
                StartWithWindowsAsAdministrator = GetBool(sections, "Settings", nameof(WallpaperConfig.StartWithWindowsAsAdministrator), false),
                CloseToTray = GetBool(sections, "Settings", nameof(WallpaperConfig.CloseToTray), true),
                ThemeMode = GetEnum(sections, "Settings", nameof(WallpaperConfig.ThemeMode), AppThemeMode.System),
                LanguageMode = GetEnum(sections, "Settings", nameof(WallpaperConfig.LanguageMode), AppLanguageMode.System),
                PlaybackEnabled = GetBool(sections, "Settings", nameof(WallpaperConfig.PlaybackEnabled), true),
                AutoTrackNewFiles = GetBool(sections, "Settings", nameof(WallpaperConfig.AutoTrackNewFiles), true),
                GlobalMute = GetBool(sections, "Settings", nameof(WallpaperConfig.GlobalMute), true),
                ThumbnailCacheEnabled = GetBool(sections, "Settings", nameof(WallpaperConfig.ThumbnailCacheEnabled), true),
                PauseVideoWhenDisplayOffOrSleeping = GetBool(sections, "Settings", nameof(WallpaperConfig.PauseVideoWhenDisplayOffOrSleeping), true),
                PreviewPopupDelaySeconds = GetInt(sections, "Settings", nameof(WallpaperConfig.PreviewPopupDelaySeconds), WallpaperConfig.DefaultPreviewPopupDelaySeconds),
            };
            config.HardwareMonitor = LoadHardwareMonitorConfig(sections);

            int monitorCount = GetInt(sections, "Settings", "MonitorCount", 0);
            for (int index = 0; index < monitorCount; index++)
            {
                string section = $"Monitor{index}";
                if (!sections.ContainsKey(section))
                {
                    continue;
                }

                config.Monitors.Add(new MonitorProfile
                {
                    Id = GetString(sections, section, nameof(MonitorProfile.Id)),
                    DisplayName = GetString(sections, section, nameof(MonitorProfile.DisplayName)),
                    FolderPath = GetString(sections, section, nameof(MonitorProfile.FolderPath)),
                    ScaleMode = GetEnum(sections, section, nameof(MonitorProfile.ScaleMode), WallpaperScaleMode.Cover),
                    Alignment = GetEnum(sections, section, nameof(MonitorProfile.Alignment), WallpaperAlignment.Center),
                    OffsetX = GetDouble(sections, section, nameof(MonitorProfile.OffsetX), 0),
                    OffsetY = GetDouble(sections, section, nameof(MonitorProfile.OffsetY), 0),
                    PlaybackOrder = GetEnum(sections, section, nameof(MonitorProfile.PlaybackOrder), PlaybackOrder.Random),
                    IntervalSeconds = GetInt(sections, section, nameof(MonitorProfile.IntervalSeconds), 60),
                    IntervalUnit = GetEnum(sections, section, nameof(MonitorProfile.IntervalUnit), TimeUnit.Seconds),
                    Transition = GetEnum(sections, section, nameof(MonitorProfile.Transition), WallpaperTransition.Fade),
                    TransitionDurationMs = GetInt(sections, section, nameof(MonitorProfile.TransitionDurationMs), 800),
                    TransitionDurationUnit = GetEnum(sections, section, nameof(MonitorProfile.TransitionDurationUnit), TimeUnit.Seconds),
                    VideoLoop = GetBool(sections, section, nameof(MonitorProfile.VideoLoop), true),
                    VideoSoundEnabled = GetBool(sections, section, nameof(MonitorProfile.VideoSoundEnabled), false),
                    PauseVideoWhenOtherAppMaximized = GetBool(sections, section, nameof(MonitorProfile.PauseVideoWhenOtherAppMaximized), true),
                    MediaFilter = GetEnum(sections, section, nameof(MonitorProfile.MediaFilter), PlaybackMediaFilter.ImagesAndVideos),
                    IncludeSubdirectories = GetBool(sections, section, nameof(MonitorProfile.IncludeSubdirectories), false),
                    IsPaused = GetBool(sections, section, nameof(MonitorProfile.IsPaused), false),
                    IsStopped = GetBool(sections, section, nameof(MonitorProfile.IsStopped), false),
                    SelectedImagePath = GetString(sections, section, nameof(MonitorProfile.SelectedImagePath)),
                });
            }

            return config;
        }
        catch
        {
            return new WallpaperConfig();
        }
    }

    public void Save(WallpaperConfig config)
    {
        string? folder = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var builder = new StringBuilder();
        AppendSection(builder, "Settings");
        AppendValue(builder, nameof(WallpaperConfig.StartWithWindows), config.StartWithWindows);
        AppendValue(builder, nameof(WallpaperConfig.StartWithWindowsAsAdministrator), config.StartWithWindowsAsAdministrator);
        AppendValue(builder, nameof(WallpaperConfig.CloseToTray), config.CloseToTray);
        AppendValue(builder, nameof(WallpaperConfig.ThemeMode), config.ThemeMode);
        AppendValue(builder, nameof(WallpaperConfig.LanguageMode), config.LanguageMode);
        AppendValue(builder, nameof(WallpaperConfig.PlaybackEnabled), config.PlaybackEnabled);
        AppendValue(builder, nameof(WallpaperConfig.AutoTrackNewFiles), config.AutoTrackNewFiles);
        AppendValue(builder, nameof(WallpaperConfig.GlobalMute), config.GlobalMute);
        AppendValue(builder, nameof(WallpaperConfig.ThumbnailCacheEnabled), config.ThumbnailCacheEnabled);
        AppendValue(builder, nameof(WallpaperConfig.PauseVideoWhenDisplayOffOrSleeping), config.PauseVideoWhenDisplayOffOrSleeping);
        AppendValue(builder, nameof(WallpaperConfig.PreviewPopupDelaySeconds), Math.Max(PreviewPopupPolicy.MinimumHoverDelaySeconds, config.PreviewPopupDelaySeconds));
        AppendValue(builder, "MonitorCount", config.Monitors.Count);
        AppendHardwareMonitorConfig(builder, config.HardwareMonitor);

        for (int index = 0; index < config.Monitors.Count; index++)
        {
            MonitorProfile monitor = config.Monitors[index];
            builder.AppendLine();
            AppendSection(builder, $"Monitor{index}");
            AppendValue(builder, nameof(MonitorProfile.Id), monitor.Id);
            AppendValue(builder, nameof(MonitorProfile.DisplayName), monitor.DisplayName);
            AppendValue(builder, nameof(MonitorProfile.FolderPath), monitor.FolderPath);
            AppendValue(builder, nameof(MonitorProfile.ScaleMode), monitor.ScaleMode);
            AppendValue(builder, nameof(MonitorProfile.Alignment), monitor.Alignment);
            AppendValue(builder, nameof(MonitorProfile.OffsetX), monitor.OffsetX);
            AppendValue(builder, nameof(MonitorProfile.OffsetY), monitor.OffsetY);
            AppendValue(builder, nameof(MonitorProfile.PlaybackOrder), monitor.PlaybackOrder);
            AppendValue(builder, nameof(MonitorProfile.IntervalSeconds), monitor.IntervalSeconds);
            AppendValue(builder, nameof(MonitorProfile.IntervalUnit), monitor.IntervalUnit);
            AppendValue(builder, nameof(MonitorProfile.Transition), monitor.Transition);
            AppendValue(builder, nameof(MonitorProfile.TransitionDurationMs), monitor.TransitionDurationMs);
            AppendValue(builder, nameof(MonitorProfile.TransitionDurationUnit), monitor.TransitionDurationUnit);
            AppendValue(builder, nameof(MonitorProfile.VideoLoop), monitor.VideoLoop);
            AppendValue(builder, nameof(MonitorProfile.VideoSoundEnabled), monitor.VideoSoundEnabled);
            AppendValue(builder, nameof(MonitorProfile.PauseVideoWhenOtherAppMaximized), monitor.PauseVideoWhenOtherAppMaximized);
            AppendValue(builder, nameof(MonitorProfile.MediaFilter), monitor.MediaFilter);
            AppendValue(builder, nameof(MonitorProfile.IncludeSubdirectories), monitor.IncludeSubdirectories);
            AppendValue(builder, nameof(MonitorProfile.IsPaused), monitor.IsPaused);
            AppendValue(builder, nameof(MonitorProfile.IsStopped), monitor.IsStopped);
            AppendValue(builder, nameof(MonitorProfile.SelectedImagePath), monitor.SelectedImagePath);
        }

        File.WriteAllText(_settingsPath, builder.ToString());
    }

    private static HardwareMonitorConfig LoadHardwareMonitorConfig(Dictionary<string, Dictionary<string, string>> sections)
    {
        const string section = "HardwareMonitor";
        int selectedSensorCount = GetInt(sections, section, "SelectedSensorCount", 0);
        var config = new HardwareMonitorConfig
        {
            IsEnabled = GetBool(sections, section, nameof(HardwareMonitorConfig.IsEnabled), false),
            RefreshIntervalSeconds = Math.Max(1, GetInt(sections, section, nameof(HardwareMonitorConfig.RefreshIntervalSeconds), HardwareMonitorConfig.DefaultRefreshIntervalSeconds)),
            TargetMonitorId = GetString(sections, section, nameof(HardwareMonitorConfig.TargetMonitorId)),
            TemplateText = DecodeString(GetString(sections, section, nameof(HardwareMonitorConfig.TemplateText)), HardwareMonitorConfig.DefaultTemplate),
            X = GetDouble(sections, section, nameof(HardwareMonitorConfig.X), 24),
            Y = GetDouble(sections, section, nameof(HardwareMonitorConfig.Y), 24),
            FontFamily = DecodeString(GetString(sections, section, nameof(HardwareMonitorConfig.FontFamily)), "Segoe UI"),
            FontSize = GetDouble(sections, section, nameof(HardwareMonitorConfig.FontSize), 16),
            Opacity = GetDouble(sections, section, nameof(HardwareMonitorConfig.Opacity), 0.88),
            BackgroundImagePath = DecodeString(GetString(sections, section, nameof(HardwareMonitorConfig.BackgroundImagePath)), string.Empty),
            BackgroundColor = DecodeString(GetString(sections, section, nameof(HardwareMonitorConfig.BackgroundColor)), string.Empty),
            SelectedElementId = GetString(sections, section, nameof(HardwareMonitorConfig.SelectedElementId)),
        };

        for (int index = 0; index < selectedSensorCount; index++)
        {
            string sensorId = DecodeString(GetString(sections, section, $"SelectedSensor{index}"), string.Empty);
            if (!string.IsNullOrWhiteSpace(sensorId))
            {
                config.SelectedSensorIds.Add(sensorId);
            }
        }

        int elementCount = GetInt(sections, section, "ElementCount", 0);
        for (int index = 0; index < elementCount; index++)
        {
            HardwareOverlayElement? element = LoadHardwareOverlayElement(sections, section, index);
            if (element is not null)
            {
                config.Elements.Add(element);
            }
        }

        if (!string.IsNullOrWhiteSpace(config.SelectedElementId)
            && !config.Elements.Any(element => string.Equals(element.Id, config.SelectedElementId, StringComparison.OrdinalIgnoreCase)))
        {
            config.SelectedElementId = string.Empty;
        }

        return config;
    }

    private static void AppendHardwareMonitorConfig(StringBuilder builder, HardwareMonitorConfig config)
    {
        builder.AppendLine();
        AppendSection(builder, "HardwareMonitor");
        AppendValue(builder, nameof(HardwareMonitorConfig.IsEnabled), config.IsEnabled);
        AppendValue(builder, nameof(HardwareMonitorConfig.RefreshIntervalSeconds), Math.Max(1, config.RefreshIntervalSeconds));
        AppendValue(builder, nameof(HardwareMonitorConfig.TargetMonitorId), config.TargetMonitorId);
        AppendValue(builder, nameof(HardwareMonitorConfig.TemplateText), EncodeString(config.TemplateText));
        AppendValue(builder, nameof(HardwareMonitorConfig.X), config.X);
        AppendValue(builder, nameof(HardwareMonitorConfig.Y), config.Y);
        AppendValue(builder, nameof(HardwareMonitorConfig.FontFamily), EncodeString(config.FontFamily));
        AppendValue(builder, nameof(HardwareMonitorConfig.FontSize), config.FontSize);
        AppendValue(builder, nameof(HardwareMonitorConfig.Opacity), config.Opacity);
        AppendValue(builder, nameof(HardwareMonitorConfig.BackgroundImagePath), EncodeString(config.BackgroundImagePath));
        AppendValue(builder, nameof(HardwareMonitorConfig.BackgroundColor), EncodeString(config.BackgroundColor));
        AppendValue(builder, nameof(HardwareMonitorConfig.SelectedElementId), config.SelectedElementId);
        AppendValue(builder, "SelectedSensorCount", config.SelectedSensorIds.Count);
        for (int index = 0; index < config.SelectedSensorIds.Count; index++)
        {
            AppendValue(builder, $"SelectedSensor{index}", EncodeString(config.SelectedSensorIds[index]));
        }

        AppendValue(builder, "ElementCount", config.Elements.Count);
        for (int index = 0; index < config.Elements.Count; index++)
        {
            AppendHardwareOverlayElement(builder, config.Elements[index], index);
        }
    }

    private static HardwareOverlayElement? LoadHardwareOverlayElement(
        Dictionary<string, Dictionary<string, string>> sections,
        string section,
        int index)
    {
        string prefix = $"Element{index}";
        string id = GetString(sections, section, $"{prefix}.Id");
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
        }

        return new HardwareOverlayElement
        {
            Id = id,
            Kind = GetEnum(sections, section, $"{prefix}.Kind", HardwareOverlayElementKind.Sensor),
            SensorId = DecodeString(GetString(sections, section, $"{prefix}.SensorId"), string.Empty),
            Text = DecodeString(GetString(sections, section, $"{prefix}.Text"), string.Empty),
            ImagePath = DecodeString(GetString(sections, section, $"{prefix}.ImagePath"), string.Empty),
            X = GetDouble(sections, section, $"{prefix}.X", 24),
            Y = GetDouble(sections, section, $"{prefix}.Y", 24),
            Width = Math.Max(20, GetDouble(sections, section, $"{prefix}.Width", 160)),
            Height = Math.Max(20, GetDouble(sections, section, $"{prefix}.Height", 40)),
            FontFamily = DecodeString(GetString(sections, section, $"{prefix}.FontFamily"), string.Empty),
            FontSize = Math.Max(0, GetDouble(sections, section, $"{prefix}.FontSize", 0)),
            Foreground = DecodeString(GetString(sections, section, $"{prefix}.Foreground"), "#FFFFFFFF"),
            Opacity = Math.Clamp(GetDouble(sections, section, $"{prefix}.Opacity", 1), 0.05, 1),
            DecimalPlaces = Math.Clamp(GetInt(sections, section, $"{prefix}.DecimalPlaces", -1), -1, 6),
        };
    }

    private static void AppendHardwareOverlayElement(StringBuilder builder, HardwareOverlayElement element, int index)
    {
        string prefix = $"Element{index}";
        AppendValue(builder, $"{prefix}.Id", element.Id);
        AppendValue(builder, $"{prefix}.Kind", element.Kind);
        AppendValue(builder, $"{prefix}.SensorId", EncodeString(element.SensorId));
        AppendValue(builder, $"{prefix}.Text", EncodeString(element.Text));
        AppendValue(builder, $"{prefix}.ImagePath", EncodeString(element.ImagePath));
        AppendValue(builder, $"{prefix}.X", element.X);
        AppendValue(builder, $"{prefix}.Y", element.Y);
        AppendValue(builder, $"{prefix}.Width", Math.Max(20, element.Width));
        AppendValue(builder, $"{prefix}.Height", Math.Max(20, element.Height));
        AppendValue(builder, $"{prefix}.FontFamily", EncodeString(element.FontFamily));
        AppendValue(builder, $"{prefix}.FontSize", Math.Max(0, element.FontSize));
        AppendValue(builder, $"{prefix}.Foreground", EncodeString(element.Foreground));
        AppendValue(builder, $"{prefix}.Opacity", Math.Clamp(element.Opacity, 0.05, 1));
        AppendValue(builder, $"{prefix}.DecimalPlaces", Math.Clamp(element.DecimalPlaces, -1, 6));
    }

    private static Dictionary<string, Dictionary<string, string>> Parse(IEnumerable<string> lines)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string currentSection = "Settings";
        sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                sections.TryAdd(currentSection, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            sections[currentSection][key] = value;
        }

        return sections;
    }

    private static string GetString(Dictionary<string, Dictionary<string, string>> sections, string section, string key, string defaultValue = "")
    {
        return sections.TryGetValue(section, out Dictionary<string, string>? values) && values.TryGetValue(key, out string? value)
            ? value
            : defaultValue;
    }

    private static bool GetBool(Dictionary<string, Dictionary<string, string>> sections, string section, string key, bool defaultValue)
    {
        return bool.TryParse(GetString(sections, section, key), out bool value) ? value : defaultValue;
    }

    private static int GetInt(Dictionary<string, Dictionary<string, string>> sections, string section, string key, int defaultValue)
    {
        return int.TryParse(GetString(sections, section, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : defaultValue;
    }

    private static double GetDouble(Dictionary<string, Dictionary<string, string>> sections, string section, string key, double defaultValue)
    {
        return double.TryParse(GetString(sections, section, key), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : defaultValue;
    }

    private static T GetEnum<T>(Dictionary<string, Dictionary<string, string>> sections, string section, string key, T defaultValue)
        where T : struct, Enum
    {
        return Enum.TryParse(GetString(sections, section, key), out T value) ? value : defaultValue;
    }

    private static void AppendSection(StringBuilder builder, string section)
    {
        builder.Append('[').Append(section).AppendLine("]");
    }

    private static void AppendValue<T>(StringBuilder builder, string key, T value)
    {
        builder.Append(key)
            .Append('=')
            .Append(Convert.ToString(value, CultureInfo.InvariantCulture))
            .AppendLine();
    }

    private static string EncodeString(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }

    private static string DecodeString(string value, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch
        {
            return defaultValue;
        }
    }

    private static string GetDefaultSettingsPath()
    {
        string? processPath = Environment.ProcessPath;
        string folder = !string.IsNullOrWhiteSpace(processPath)
            ? Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory
            : AppContext.BaseDirectory;
        return Path.Combine(folder, SettingsFileName);
    }
}
