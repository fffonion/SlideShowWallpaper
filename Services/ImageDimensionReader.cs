using System.Buffers.Binary;

namespace SlideShowWallpaper.Services;

public static class ImageDimensionReader
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool TryRead(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            return TryReadPng(stream, out width, out height)
                || TryReadJpeg(stream, out width, out height)
                || TryReadBmp(stream, out width, out height)
                || TryReadWebp(stream, out width, out height);
        }
        catch (Exception exception)
        {
            AppLog.Write(exception);
            width = 0;
            height = 0;
            return false;
        }
    }

    private static bool TryReadPng(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        Span<byte> header = stackalloc byte[24];
        stream.Position = 0;
        if (stream.Read(header) != header.Length
            || !HasSignature(header[..8], PngSignature))
        {
            return false;
        }

        width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
        height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
        return width > 0 && height > 0;
    }

    private static bool TryReadJpeg(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        stream.Position = 0;
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8)
        {
            return false;
        }

        while (stream.Position < stream.Length)
        {
            int markerPrefix = ReadUntilMarkerPrefix(stream);
            if (markerPrefix < 0)
            {
                return false;
            }

            int marker = stream.ReadByte();
            while (marker == 0xFF)
            {
                marker = stream.ReadByte();
            }

            if (marker < 0 || marker is 0xD9 or 0xDA)
            {
                return false;
            }

            int segmentLength = ReadBigEndianUInt16(stream);
            if (segmentLength < 2 || stream.Position + segmentLength - 2 > stream.Length)
            {
                return false;
            }

            if (IsJpegStartOfFrame(marker))
            {
                _ = stream.ReadByte();
                height = ReadBigEndianUInt16(stream);
                width = ReadBigEndianUInt16(stream);
                return width > 0 && height > 0;
            }

            stream.Position += segmentLength - 2;
        }

        return false;
    }

    private static bool TryReadBmp(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        Span<byte> header = stackalloc byte[26];
        stream.Position = 0;
        if (stream.Read(header) != header.Length || header[0] != 0x42 || header[1] != 0x4D)
        {
            return false;
        }

        width = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(header[18..22]));
        height = Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(header[22..26]));
        return width > 0 && height > 0;
    }

    private static bool TryReadWebp(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        Span<byte> header = stackalloc byte[30];
        stream.Position = 0;
        if (stream.Read(header) < 30
            || !HasSignature(header[..4], "RIFF"u8)
            || !HasSignature(header[8..12], "WEBP"u8))
        {
            return false;
        }

        if (HasSignature(header[12..16], "VP8X"u8))
        {
            width = ReadWebp24BitDimension(header[24..27]) + 1;
            height = ReadWebp24BitDimension(header[27..30]) + 1;
            return width > 0 && height > 0;
        }

        if (HasSignature(header[12..16], "VP8 "u8) && header[23] == 0x9D && header[24] == 0x01 && header[25] == 0x2A)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(header[26..28]) & 0x3FFF;
            height = BinaryPrimitives.ReadUInt16LittleEndian(header[28..30]) & 0x3FFF;
            return width > 0 && height > 0;
        }

        if (HasSignature(header[12..16], "VP8L"u8) && header[20] == 0x2F)
        {
            uint bits = BinaryPrimitives.ReadUInt32LittleEndian(header[21..25]);
            width = (int)(bits & 0x3FFF) + 1;
            height = (int)((bits >> 14) & 0x3FFF) + 1;
            return width > 0 && height > 0;
        }

        return false;
    }

    private static int ReadUntilMarkerPrefix(Stream stream)
    {
        int value;
        do
        {
            value = stream.ReadByte();
        }
        while (value >= 0 && value != 0xFF);

        return value;
    }

    private static int ReadBigEndianUInt16(Stream stream)
    {
        Span<byte> bytes = stackalloc byte[2];
        return stream.Read(bytes) == bytes.Length
            ? BinaryPrimitives.ReadUInt16BigEndian(bytes)
            : -1;
    }

    private static bool IsJpegStartOfFrame(int marker)
    {
        return marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;
    }

    private static int ReadWebp24BitDimension(ReadOnlySpan<byte> bytes)
    {
        return bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
    }

    private static bool HasSignature(ReadOnlySpan<byte> value, ReadOnlySpan<byte> signature)
    {
        if (value.Length < signature.Length)
        {
            return false;
        }

        for (int index = 0; index < signature.Length; index++)
        {
            if (value[index] != signature[index])
            {
                return false;
            }
        }

        return true;
    }
}
