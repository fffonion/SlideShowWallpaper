using SlideShowWallpaper.Models;

namespace SlideShowWallpaper.Services;

public sealed record MonitorProfileChange(bool QueueChanged, bool VisualChanged, bool PlaybackSettingsChanged = false)
{
    public bool HasChanges => QueueChanged || VisualChanged || PlaybackSettingsChanged;
}

public sealed class MonitorProfileChangeTracker
{
    private readonly Dictionary<string, Snapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public MonitorProfileChange Update(MonitorProfile profile)
    {
        var next = Snapshot.From(profile);
        if (!_snapshots.TryGetValue(profile.Id, out Snapshot? previous))
        {
            _snapshots[profile.Id] = next;
            return new MonitorProfileChange(true, false);
        }

        _snapshots[profile.Id] = next;
        bool queueChanged = !string.Equals(previous.FolderPath, next.FolderPath, StringComparison.OrdinalIgnoreCase)
            || previous.PlaybackOrder != next.PlaybackOrder
            || previous.MediaFilter != next.MediaFilter;
        bool visualChanged = !queueChanged
            && (previous.ScaleMode != next.ScaleMode
                || !previous.OffsetX.Equals(next.OffsetX)
                || !previous.OffsetY.Equals(next.OffsetY));
        bool playbackSettingsChanged = !queueChanged
            && !visualChanged
            && (previous.IntervalSeconds != next.IntervalSeconds
                || previous.Transition != next.Transition
                || previous.TransitionDurationMs != next.TransitionDurationMs
                || previous.VideoLoop != next.VideoLoop);

        return new MonitorProfileChange(queueChanged, visualChanged, playbackSettingsChanged);
    }

    public void Forget(string monitorId)
    {
        _snapshots.Remove(monitorId);
    }

    public void Clear()
    {
        _snapshots.Clear();
    }

    private sealed record Snapshot(
        string FolderPath,
        PlaybackOrder PlaybackOrder,
        WallpaperScaleMode ScaleMode,
        double OffsetX,
        double OffsetY,
        int IntervalSeconds,
        WallpaperTransition Transition,
        int TransitionDurationMs,
        bool VideoLoop,
        PlaybackMediaFilter MediaFilter)
    {
        public static Snapshot From(MonitorProfile profile)
        {
            return new Snapshot(
                profile.FolderPath,
                profile.PlaybackOrder,
                profile.ScaleMode,
                profile.OffsetX,
                profile.OffsetY,
                profile.IntervalSeconds,
                profile.Transition,
                profile.TransitionDurationMs,
                profile.VideoLoop,
                profile.MediaFilter);
        }
    }
}
