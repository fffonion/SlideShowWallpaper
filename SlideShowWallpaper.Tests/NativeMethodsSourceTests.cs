namespace SlideShowWallpaper.Tests;

public sealed class NativeMethodsSourceTests
{
    [Fact]
    public void LayeredTransparentWindow_UsesColorKeyAndAlpha()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Interop", "NativeMethods.cs"));

        Assert.Contains("WS_EX_LAYERED", source);
        Assert.Contains("LWA_COLORKEY | LWA_ALPHA", source);
        Assert.Contains("SetLayeredWindowAttributes(hWnd, ComposeColorRef(1, 2, 3), alpha", source);
        Assert.Contains("return (uint)(red | (green << 8) | (blue << 16));", source);
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
