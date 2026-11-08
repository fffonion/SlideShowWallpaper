using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SlideShowWallpaper.Tests;

internal static class ImageTestFiles
{
    internal static async Task<string> CreateAsync(string folder, string name, Guid encoderId, ushort orientation = 1)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, name);
        using FileStream file = File.Create(path);
        using IRandomAccessStream stream = file.AsRandomAccessStream();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 40, 80, 96, 96, new byte[40 * 80 * 4]);
        if (orientation != 1)
        {
            await encoder.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet
            {
                ["System.Photo.Orientation"] = new BitmapTypedValue(orientation, PropertyType.UInt16),
            });
        }

        await encoder.FlushAsync();
        return path;
    }
}
