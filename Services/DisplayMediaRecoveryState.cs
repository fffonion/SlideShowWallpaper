namespace SlideShowWallpaper.Services;

public sealed class DisplayMediaRecoveryState
{
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);

    public void Request(IEnumerable<string> monitorIds) => _pending.UnionWith(monitorIds);

    public bool Take(string monitorId, bool isMonitorAvailable) => isMonitorAvailable && _pending.Remove(monitorId);

    public void Clear() => _pending.Clear();
}
