namespace SlideShowWallpaper.Tests;

public sealed class FolderPickerServiceSourceTests
{
    [Fact]
    public void PickerImplementation_KeepsIFileDialogForNativeFileSystemOptions()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "FolderPickerService.cs"));

        Assert.Contains("IFileDialog", source);
        Assert.Contains("FOS_FORCEFILESYSTEM", source);
        Assert.Contains("FOS_NOCHANGEDIR", source);
        Assert.Contains("new COMDLG_FILTERSPEC(\"All files (*.*)\", \"*.*\")", source);
        Assert.Contains("SetDefaultExtension", source);
        Assert.DoesNotContain("Microsoft.Windows.Storage.Pickers", source);
    }

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

        Assert.Contains("ShowOpenFileDialog(ownerHwnd, fileTypes)", method);
        Assert.Contains("IFileDialog", source);
        Assert.Contains("FOS_FILEMUSTEXIST", source);
        Assert.Contains("BuildFileTypeFilters(fileTypes)", source);
        Assert.DoesNotContain("FileOpenPicker", method);
        Assert.DoesNotContain("OPENFILENAME", source);
        Assert.DoesNotContain("GetOpenFileName", source);
    }

    [Fact]
    public void PickSaveFileAsync_UsesNativeSaveFileDialog()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "FolderPickerService.cs"));
        int methodStart = FindPickSaveFileAsyncStart(source);
        int methodEnd = source.IndexOf(
            "private static string? ShowFolderDialog",
            StringComparison.Ordinal);
        string method = source[methodStart..methodEnd];

        Assert.Contains("ShowSaveFileDialog(ownerHwnd, fileType, defaultFileName)", method);
        Assert.Contains("IFileDialog", source);
        Assert.Contains("FOS_OVERWRITEPROMPT", source);
        Assert.Contains("BuildFileTypeFilters([fileType])", source);
        Assert.Contains("NormalizeDialogExtension(fileType)", source);
        Assert.DoesNotContain("FileSavePicker", method);
        Assert.DoesNotContain("OPENFILENAME", source);
        Assert.DoesNotContain("GetSaveFileName", source);
    }

    [Fact]
    public void PickFolderAsync_UsesNativeFolderDialog()
    {
        string root = FindProjectRoot();
        string source = File.ReadAllText(Path.Combine(root, "Services", "FolderPickerService.cs"));
        string method = source[
            FindPickFolderAsyncStart(source)..
            source.IndexOf("public async Task<string?> PickOpenFileAsync", StringComparison.Ordinal)];

        Assert.Contains("ShowFolderDialog(ownerHwnd)", method);
        Assert.Contains("FOS_PICKFOLDERS", source);
        Assert.Contains("FOS_FORCEFILESYSTEM", source);
        Assert.DoesNotContain("FolderPicker", method);
    }

    private static int FindPickFolderAsyncStart(string source)
    {
        int methodStart = source.IndexOf(
            "public Task<string?> PickFolderAsync(IntPtr ownerHwnd)",
            StringComparison.Ordinal);
        if (methodStart >= 0)
        {
            return methodStart;
        }

        return source.IndexOf(
            "public async Task<string?> PickFolderAsync(IntPtr ownerHwnd)",
            StringComparison.Ordinal);
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
