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

    public Task<string?> PickSaveFileAsync(IntPtr ownerHwnd, string fileType, string defaultFileName)
    {
        return Task.FromResult(PickSaveFileNative(ownerHwnd, fileType, defaultFileName));
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

    private static string? PickSaveFileNative(IntPtr ownerHwnd, string fileType, string defaultFileName)
    {
        try
        {
            if (ownerHwnd != IntPtr.Zero)
            {
                NativeMethods.SetForegroundWindow(ownerHwnd);
            }

            string extension = NormalizeDialogExtension(fileType);
            string suggestedFileName = BuildSuggestedSaveFileName(defaultFileName, extension);
            var fileName = new StringBuilder(suggestedFileName, OpenFilePathBufferLength);
            var openFileName = new NativeMethods.OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<NativeMethods.OPENFILENAME>(),
                hwndOwner = ownerHwnd,
                lpstrFilter = BuildSaveFileFilter(fileType),
                lpstrFile = fileName,
                nMaxFile = fileName.Capacity,
                nFilterIndex = 1,
                lpstrDefExt = extension,
                Flags = NativeMethods.OFN_EXPLORER
                    | NativeMethods.OFN_OVERWRITEPROMPT
                    | NativeMethods.OFN_PATHMUSTEXIST
                    | NativeMethods.OFN_NOCHANGEDIR,
            };

            if (NativeMethods.GetSaveFileName(ref openFileName))
            {
                return fileName.ToString();
            }

            int error = NativeMethods.CommDlgExtendedError();
            if (error != 0)
            {
                AppLog.Write($"GetSaveFileName failed: 0x{error:X}");
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

    private static string BuildSaveFileFilter(string fileType)
    {
        string extension = NormalizeDialogExtension(fileType);
        string pattern = string.IsNullOrWhiteSpace(extension) ? "*.*" : $"*.{extension}";
        string label = string.IsNullOrWhiteSpace(extension) ? pattern : $"{extension.ToUpperInvariant()} files ({pattern})";
        return $"{label}\0{pattern}\0\0";
    }

    private static string BuildSuggestedSaveFileName(string defaultFileName, string extension)
    {
        string fileName = string.IsNullOrWhiteSpace(defaultFileName) ? "untitled" : defaultFileName.Trim();
        if (string.IsNullOrWhiteSpace(extension) || Path.HasExtension(fileName))
        {
            return fileName;
        }

        return $"{fileName}.{extension}";
    }

    private static string NormalizeDialogExtension(string fileType)
    {
        string extension = fileType.Trim();
        if (extension.StartsWith('*'))
        {
            extension = extension.TrimStart('*');
        }

        return extension.TrimStart('.');
    }
}
