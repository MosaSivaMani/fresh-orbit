using System.Buffers.Binary;

namespace FreshOrbit.Catalog.Tests.Support;

/// <summary>SEC-5 test input: a real small PNG whose IHDR claims other dimensions (decompression bomb header).</summary>
internal static class PngHeader
{
    /// <summary>Rewrites the IHDR width and height (bytes 16–23) and its CRC (bytes 29–32); the pixel data stays small.</summary>
    public static byte[] Claiming(byte[] png, int width, int height)
    {
        var bytes = (byte[])png.Clone();
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(29, 4), Crc32(bytes.AsSpan(12, 17)));
        return bytes;
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }
}
