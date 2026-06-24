namespace SlideShowWallpaper.Tests;

public sealed class FolderPickerServiceSourceTests
{
    [Fact]
    public void PickOpenFileAsync_UsesNativeOpenFileDialog()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "FolderPickerService.cs"));
        int methodStart = source.IndexOf(
            "public Task<string?> PickOpenFileAsync(IntPtr ownerHwnd, IReadOnlyList<string> fileTypes)",
            StringComparison.Ordinal);
        int methodEnd = FindPickSaveFileAsyncStart(source);
        string method = source[methodStart..methodEnd];

        Assert.Contains("PickOpenFileNative(ownerHwnd, fileTypes)", method);
        Assert.Contains("NativeMethods.GetOpenFileName", source);
        Assert.Contains("NativeMethods.CommDlgExtendedError", source);
        Assert.Contains("NativeMethods.OFN_FILEMUSTEXIST", source);
        Assert.Contains("BuildOpenFileFilter(fileTypes)", source);
        Assert.DoesNotContain("FileOpenPicker", method);
    }

    [Fact]
    public void PickSaveFileAsync_UsesNativeSaveFileDialog()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "FolderPickerService.cs"));
        int methodStart = FindPickSaveFileAsyncStart(source);
        int methodEnd = source.IndexOf(
            "private static string? PickOpenFileNative",
            StringComparison.Ordinal);
        string method = source[methodStart..methodEnd];

        Assert.Contains("PickSaveFileNative(ownerHwnd, fileType, defaultFileName)", method);
        Assert.Contains("NativeMethods.GetSaveFileName", source);
        Assert.Contains("NativeMethods.OFN_OVERWRITEPROMPT", source);
        Assert.Contains("NativeMethods.CommDlgExtendedError", source);
        Assert.Contains("BuildSaveFileFilter(fileType)", source);
        Assert.Contains("NormalizeDialogExtension(fileType)", source);
        Assert.DoesNotContain("FileSavePicker", method);
    }

    private static int FindPickSaveFileAsyncStart(string source)
    {
        int methodStart = source.IndexOf(
            "public Task<string?> PickSaveFileAsync(IntPtr ownerHwnd, string fileType, string defaultFileName)",
            StringComparison.Ordinal);
        if (methodStart >= 0)
        {
            return methodStart;
        }

        return source.IndexOf(
            "public async Task<string?> PickSaveFileAsync(IntPtr ownerHwnd, string fileType, string defaultFileName)",
            StringComparison.Ordinal);
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
