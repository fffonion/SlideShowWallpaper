namespace SlideShowWallpaper.Services;

public static class FileLinkResolver
{
    public static FileInfo GetFinalFileInfo(string path)
    {
        var file = new FileInfo(path);
        try
        {
            if (file.ResolveLinkTarget(true) is FileInfo target && target.Exists)
            {
                return target;
            }
        }
        catch (IOException exception)
        {
            AppLog.Write(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            AppLog.Write(exception);
        }

        return file;
    }

    public static string GetFinalPath(string path)
    {
        return GetFinalFileInfo(path).FullName;
    }
}
