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
                CloseToTray = GetBool(sections, "Settings", nameof(WallpaperConfig.CloseToTray), true),
                ThemeMode = GetEnum(sections, "Settings", nameof(WallpaperConfig.ThemeMode), AppThemeMode.System),
                PlaybackEnabled = GetBool(sections, "Settings", nameof(WallpaperConfig.PlaybackEnabled), true),
            };

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
                    VideoLoop = GetBool(sections, section, nameof(MonitorProfile.VideoLoop), false),
                    VideoSoundEnabled = GetBool(sections, section, nameof(MonitorProfile.VideoSoundEnabled), false),
                    MediaFilter = GetEnum(sections, section, nameof(MonitorProfile.MediaFilter), PlaybackMediaFilter.ImagesAndVideos),
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
        AppendValue(builder, nameof(WallpaperConfig.CloseToTray), config.CloseToTray);
        AppendValue(builder, nameof(WallpaperConfig.ThemeMode), config.ThemeMode);
        AppendValue(builder, nameof(WallpaperConfig.PlaybackEnabled), config.PlaybackEnabled);
        AppendValue(builder, "MonitorCount", config.Monitors.Count);

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
            AppendValue(builder, nameof(MonitorProfile.MediaFilter), monitor.MediaFilter);
            AppendValue(builder, nameof(MonitorProfile.IsPaused), monitor.IsPaused);
            AppendValue(builder, nameof(MonitorProfile.IsStopped), monitor.IsStopped);
            AppendValue(builder, nameof(MonitorProfile.SelectedImagePath), monitor.SelectedImagePath);
        }

        File.WriteAllText(_settingsPath, builder.ToString());
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

    private static string GetDefaultSettingsPath()
    {
        string? processPath = Environment.ProcessPath;
        string folder = !string.IsNullOrWhiteSpace(processPath)
            ? Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory
            : AppContext.BaseDirectory;
        return Path.Combine(folder, SettingsFileName);
    }
}
