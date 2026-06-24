using System.Runtime.InteropServices;

namespace SlideShowWallpaper.Services;

public static class FontCatalogService
{
    private const byte DefaultCharSet = 1;
    private static readonly object SyncRoot = new();
    private static IReadOnlyList<string>? _cachedFontFamilies;
    private static int _cacheVersion;

    public static IReadOnlyList<string> GetInstalledFontFamilies()
    {
        int version;
        lock (SyncRoot)
        {
            if (_cachedFontFamilies is not null)
            {
                return _cachedFontFamilies;
            }

            version = _cacheVersion;
        }

        IReadOnlyList<string> fonts = EnumerateInstalledFontFamilies();
        lock (SyncRoot)
        {
            if (version == _cacheVersion)
            {
                _cachedFontFamilies ??= fonts;
                return _cachedFontFamilies;
            }

            return fonts;
        }
    }

    public static void ClearCache()
    {
        lock (SyncRoot)
        {
            _cachedFontFamilies = null;
            _cacheVersion++;
        }
    }

    private static IReadOnlyList<string> EnumerateInstalledFontFamilies()
    {
        var names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
        IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var logFont = new LogFont
            {
                lfCharSet = DefaultCharSet,
                lfFaceName = string.Empty,
            };
            EnumFontFamExProc callback = (ref LogFont font, IntPtr _, uint _, IntPtr _) =>
            {
                string name = font.lfFaceName.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith('@'))
                {
                    names.Add(name);
                }

                return 1;
            };
            _ = EnumFontFamiliesEx(hdc, ref logFont, callback, IntPtr.Zero, 0);
        }
        finally
        {
            _ = DeleteDC(hdc);
        }

        return names.ToArray();
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int EnumFontFamiliesEx(IntPtr hdc, ref LogFont lpLogfont, EnumFontFamExProc lpProc, IntPtr lParam, uint dwFlags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(IntPtr hdc);

    private delegate int EnumFontFamExProc(ref LogFont lpelfe, IntPtr lpntme, uint fontType, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct LogFont
    {
        public int lfHeight;
        public int lfWidth;
        public int lfEscapement;
        public int lfOrientation;
        public int lfWeight;
        public byte lfItalic;
        public byte lfUnderline;
        public byte lfStrikeOut;
        public byte lfCharSet;
        public byte lfOutPrecision;
        public byte lfClipPrecision;
        public byte lfQuality;
        public byte lfPitchAndFamily;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string lfFaceName;
    }
}
