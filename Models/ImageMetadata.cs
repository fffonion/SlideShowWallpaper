namespace SlideShowWallpaper.Models;

public sealed record ImageMetadata(string Path, string FileName, DateTime ModifiedUtc, long Length, MediaKind Kind = MediaKind.Image);
