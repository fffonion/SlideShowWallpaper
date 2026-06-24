using System.Runtime.InteropServices;
using System.Text;

using SlideShowWallpaper.Interop;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace SlideShowWallpaper.Services;

public sealed class FolderPickerService
{
    private const int OpenFilePathBufferLength = 32768;

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
        return await PickOpenFileAsync(ownerHwnd, [fileType]);
    }

    public Task<string?> PickOpenFileAsync(IntPtr ownerHwnd, IReadOnlyList<string> fileTypes)
    {
        return Task.FromResult(PickOpenFileNative(ownerHwnd, fileTypes));
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

    private static string? PickOpenFileNative(IntPtr ownerHwnd, IReadOnlyList<string> fileTypes)
    {
        try
        {
            if (ownerHwnd != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(ownerHwnd);
            }

            var fileName = new StringBuilder(OpenFilePathBufferLength);
            var openFileName = new NativeMethods.OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<NativeMethods.OPENFILENAME>(),
                hwndOwner = ownerHwnd,
                lpstrFilter = BuildOpenFileFilter(fileTypes),
                lpstrFile = fileName,
                nMaxFile = fileName.Capacity,
                nFilterIndex = 1,
                Flags = NativeMethods.OFN_EXPLORER
                    | NativeMethods.OFN_FILEMUSTEXIST
                    | NativeMethods.OFN_PATHMUSTEXIST
                    | NativeMethods.OFN_NOCHANGEDIR,
            };

            if (NativeMethods.GetOpenFileName(ref openFileName))
            {
                return fileName.ToString();
            }

            int error = NativeMethods.CommDlgExtendedError();
            if (error != 0)
            {
                AppLog.Write($"GetOpenFileName failed: 0x{error:X}");
            }
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
        }

        return null;
    }

    private static string BuildOpenFileFilter(IReadOnlyList<string> fileTypes)
    {
        string pattern = string.Join(
            ';',
            fileTypes
                .Where(fileType => !string.IsNullOrWhiteSpace(fileType))
                .Select(fileType => fileType.Trim())
                .Select(fileType => fileType.StartsWith('*') ? fileType : fileType.StartsWith('.') ? $"*{fileType}" : $"*.{fileType.TrimStart('.')}")
                .Distinct(StringComparer.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "*.*";
        }

        return $"{pattern}\0{pattern}\0\0";
    }
}
