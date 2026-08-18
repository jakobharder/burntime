using System.IO.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BurnGfxRipper;

internal static class PngWriter
{
    public static void SaveBgra(byte[] pixels, int width, int height, string path)
    {
        using FileStream output = File.Create(path);
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        WriteChunk(output, "IHDR", [.. Int32(width), .. Int32(height), 8, 6, 0, 0, 0]);
        byte[] rows = new byte[height * (width * 4 + 1)];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int source = (y * width + x) * 4;
                int destination = y * (width * 4 + 1) + 1 + x * 4;
                rows[destination] = pixels[source + 2]; rows[destination + 1] = pixels[source + 1];
                rows[destination + 2] = pixels[source]; rows[destination + 3] = pixels[source + 3];
            }
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, true)) zlib.Write(rows);
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
    }

    private static byte[] Int32(int value) => [unchecked((byte)(value >> 24)), unchecked((byte)(value >> 16)), unchecked((byte)(value >> 8)), unchecked((byte)value)];
    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        output.Write(Int32(data.Length)); output.Write(typeBytes); output.Write(data); output.Write(Int32(unchecked((int)Crc(typeBytes.Concat(data)))));
    }
    private static uint Crc(IEnumerable<byte> values)
    {
        uint crc = 0xffffffff;
        foreach (byte value in values) { crc ^= value; for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320 : 0); }
        return ~crc;
    }
}
