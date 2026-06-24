namespace SlideShowWallpaper.Models;

public sealed record ImageOrderLoadResult(IReadOnlyList<ImageMetadata> Images, bool LoadedFromCache);
