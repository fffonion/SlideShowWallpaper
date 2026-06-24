using System.Runtime.InteropServices;

namespace SlideShowWallpaper.Services;

public sealed class FolderPickerService
{
    private const int S_OK = 0;
    private const int ERROR_CANCELLED = unchecked((int)0x800704C7);
    private const uint FOS_OVERWRITEPROMPT = 0x00000002;
    private const uint FOS_PICKFOLDERS = 0x00000020;
    private const uint FOS_FORCEFILESYSTEM = 0x00000040;
    private const uint FOS_PATHMUSTEXIST = 0x00000800;
    private const uint FOS_FILEMUSTEXIST = 0x00001000;
    private const uint FOS_NOCHANGEDIR = 0x00000008;
    private const uint FOS_NOREADONLYRETURN = 0x00008000;
    private const uint SIGDN_FILESYSPATH = 0x80058000;

    public Task<string?> PickFolderAsync(IntPtr ownerHwnd)
    {
        return Task.FromResult(ShowFolderDialog(ownerHwnd));
    }

    public async Task<string?> PickOpenFileAsync(IntPtr ownerHwnd, string fileType)
    {
        return await PickOpenFileAsync(ownerHwnd, [fileType]);
    }

    public Task<string?> PickOpenFileAsync(IntPtr ownerHwnd, IReadOnlyList<string> fileTypes)
    {
        return Task.FromResult(ShowOpenFileDialog(ownerHwnd, fileTypes));
    }

    public Task<string?> PickSaveFileAsync(IntPtr ownerHwnd, string fileType, string defaultFileName)
    {
        return Task.FromResult(ShowSaveFileDialog(ownerHwnd, fileType, defaultFileName));
    }

    private static string? ShowFolderDialog(IntPtr ownerHwnd)
    {
        return ShowFileDialog(
            createDialog: () => new FileOpenDialog(),
            ownerHwnd,
            optionsToAdd: FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_NOCHANGEDIR,
            configureDialog: null);
    }

    private static string? ShowOpenFileDialog(IntPtr ownerHwnd, IReadOnlyList<string> fileTypes)
    {
        return ShowFileDialog(
            createDialog: () => new FileOpenDialog(),
            ownerHwnd,
            optionsToAdd: FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_FILEMUSTEXIST | FOS_NOCHANGEDIR,
            configureDialog: dialog =>
            {
                COMDLG_FILTERSPEC[] filters = BuildFileTypeFilters(fileTypes);
                dialog.SetFileTypes((uint)filters.Length, filters);
                dialog.SetFileTypeIndex(1);
            });
    }

    private static string? ShowSaveFileDialog(IntPtr ownerHwnd, string fileType, string defaultFileName)
    {
        return ShowFileDialog(
            createDialog: () => new FileSaveDialog(),
            ownerHwnd,
            optionsToAdd: FOS_OVERWRITEPROMPT | FOS_NOREADONLYRETURN | FOS_FORCEFILESYSTEM | FOS_PATHMUSTEXIST | FOS_NOCHANGEDIR,
            configureDialog: dialog =>
            {
                string extension = NormalizeDialogExtension(fileType);
                COMDLG_FILTERSPEC[] filters = BuildFileTypeFilters([fileType]);
                dialog.SetFileTypes((uint)filters.Length, filters);
                dialog.SetFileTypeIndex(1);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    dialog.SetDefaultExtension(extension);
                }

                dialog.SetFileName(BuildSuggestedSaveFileName(defaultFileName, extension));
            });
    }

    private static string? ShowFileDialog(Func<object> createDialog, IntPtr ownerHwnd, uint optionsToAdd, Action<IFileDialog>? configureDialog)
    {
        object? dialogObject = null;
        IShellItem? result = null;
        try
        {
            dialogObject = createDialog();
            var dialog = (IFileDialog)dialogObject;
            ThrowIfFailed(dialog.GetOptions(out uint options));
            ThrowIfFailed(dialog.SetOptions(options | optionsToAdd));
            configureDialog?.Invoke(dialog);

            int showResult = dialog.Show(ownerHwnd);
            if (showResult == ERROR_CANCELLED)
            {
                return null;
            }

            ThrowIfFailed(showResult);
            ThrowIfFailed(dialog.GetResult(out result));
            return GetShellItemPath(result);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            return null;
        }
        finally
        {
            if (result is not null)
            {
                Marshal.FinalReleaseComObject(result);
            }

            if (dialogObject is not null)
            {
                Marshal.FinalReleaseComObject(dialogObject);
            }
        }
    }

    private static COMDLG_FILTERSPEC[] BuildFileTypeFilters(IReadOnlyList<string> fileTypes)
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

        return
        [
            new COMDLG_FILTERSPEC(pattern, pattern),
            new COMDLG_FILTERSPEC("All files (*.*)", "*.*"),
        ];
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

    private static string? GetShellItemPath(IShellItem item)
    {
        ThrowIfFailed(item.GetDisplayName(SIGDN_FILESYSPATH, out IntPtr pathPointer));
        try
        {
            return Marshal.PtrToStringUni(pathPointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pathPointer);
        }
    }

    private static void ThrowIfFailed(int hresult)
    {
        if (hresult != S_OK)
        {
            Marshal.ThrowExceptionForHR(hresult);
        }
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    private sealed class FileOpenDialog;

    [ComImport]
    [Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    private sealed class FileSaveDialog;

    [ComImport]
    [Guid("42F85136-DB7E-439C-85F1-E4075D135FC8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig]
        int Show(IntPtr hwndOwner);

        [PreserveSig]
        int SetFileTypes(uint cFileTypes, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] COMDLG_FILTERSPEC[] rgFilterSpec);

        [PreserveSig]
        int SetFileTypeIndex(uint iFileType);

        [PreserveSig]
        int GetFileTypeIndex(out uint piFileType);

        [PreserveSig]
        int Advise(IntPtr pfde, out uint pdwCookie);

        [PreserveSig]
        int Unadvise(uint dwCookie);

        [PreserveSig]
        int SetOptions(uint fos);

        [PreserveSig]
        int GetOptions(out uint pfos);

        [PreserveSig]
        int SetDefaultFolder(IShellItem psi);

        [PreserveSig]
        int SetFolder(IShellItem psi);

        [PreserveSig]
        int GetFolder(out IShellItem ppsi);

        [PreserveSig]
        int GetCurrentSelection(out IShellItem ppsi);

        [PreserveSig]
        int SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);

        [PreserveSig]
        int GetFileName(out IntPtr pszName);

        [PreserveSig]
        int SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

        [PreserveSig]
        int SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);

        [PreserveSig]
        int SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        [PreserveSig]
        int GetResult(out IShellItem ppsi);

        [PreserveSig]
        int AddPlace(IShellItem psi, int fdap);

        [PreserveSig]
        int SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

        [PreserveSig]
        int Close(int hr);

        [PreserveSig]
        int SetClientGuid(ref Guid guid);

        [PreserveSig]
        int ClearClientData();

        [PreserveSig]
        int SetFilter(IntPtr pFilter);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        [PreserveSig]
        int BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int GetParent(out IShellItem ppsi);

        [PreserveSig]
        int GetDisplayName(uint sigdnName, out IntPtr ppszName);

        [PreserveSig]
        int GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

        [PreserveSig]
        int Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private readonly struct COMDLG_FILTERSPEC(string name, string spec)
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public readonly string Name = name;

        [MarshalAs(UnmanagedType.LPWStr)]
        public readonly string Spec = spec;
    }
}
