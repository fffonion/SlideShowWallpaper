using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SlideShowWallpaper.Services;

public sealed class FolderPickerService
{
    public async Task<string?> PickFolderAsync(IntPtr ownerHwnd)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, ownerHwnd);
        global::Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<string?> PickOpenFileAsync(IntPtr ownerHwnd, string fileType)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(fileType);
        InitializeWithWindow.Initialize(picker, ownerHwnd);
        global::Windows.Storage.StorageFile? file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    public async Task<string?> PickSaveFileAsync(IntPtr ownerHwnd, string fileType, string defaultFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = defaultFileName,
        };
        picker.FileTypeChoices.Add(fileType.TrimStart('.').ToUpperInvariant(), [fileType]);
        InitializeWithWindow.Initialize(picker, ownerHwnd);
        global::Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}
