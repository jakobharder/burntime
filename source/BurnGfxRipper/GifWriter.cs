using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Burntime.Platform.Graphics;

namespace BurnGfxRipper;

internal sealed record GifFrame(byte[] Bgra, int Width, int Height);

internal static class GifWriter
{
    private const int TransparentIndex = 255;
    private const ushort FrameDelay = 20;

    public static void Save(IReadOnlyList<GifFrame> frames, string path, ushort frameDelay = FrameDelay)
    {
        if (frames.Count == 0)
            return;

        Dictionary<int, byte> colorIndices = BuildColorTable(frames);
        byte[] colorTable = new byte[256 * 3];
        foreach ((int color, byte index) in colorIndices)
        {
            colorTable[index * 3] = (byte)(color >> 16);
            colorTable[index * 3 + 1] = (byte)(color >> 8);
            colorTable[index * 3 + 2] = (byte)color;
        }

        int canvasWidth = frames.Max(frame => frame.Width);
        int canvasHeight = frames.Max(frame => frame.Height);
        using FileStream output = File.Create(path);
        output.Write(Encoding.ASCII.GetBytes("GIF89a"));
        WriteUInt16(output, canvasWidth);
        WriteUInt16(output, canvasHeight);
        output.WriteByte(0xf7); // global 256-color table, 8-bit color resolution
        output.WriteByte(TransparentIndex);
        output.WriteByte(0);
        output.Write(colorTable);
        WriteLoopExtension(output);

        foreach (GifFrame frame in frames)
        {
            WriteGraphicControlExtension(output, frameDelay);
            output.WriteByte(0x2c);
            WriteUInt16(output, 0);
            WriteUInt16(output, 0);
            WriteUInt16(output, frame.Width);
            WriteUInt16(output, frame.Height);
            output.WriteByte(0);

            byte[] indices = ConvertPixels(frame, colorIndices);
            output.WriteByte(8);
            WriteSubBlocks(output, CompressLzw(indices));
        }

        output.WriteByte(0x3b);
    }

    private static Dictionary<int, byte> BuildColorTable(IEnumerable<GifFrame> frames)
    {
        Dictionary<int, byte> result = new();
        foreach (GifFrame frame in frames)
            for (int offset = 0; offset < frame.Bgra.Length; offset += 4)
            {
                if (frame.Bgra[offset + 3] == 0)
                    continue;
                int color = frame.Bgra[offset + 2] << 16 | frame.Bgra[offset + 1] << 8 | frame.Bgra[offset];
                if (!result.ContainsKey(color))
                {
                    if (result.Count == TransparentIndex)
                        throw new InvalidDataException("Animation uses more than 255 opaque colors.");
                    result.Add(color, (byte)result.Count);
                }
            }
        return result;
    }

    private static byte[] ConvertPixels(GifFrame frame, IReadOnlyDictionary<int, byte> colors)
    {
        byte[] result = new byte[frame.Width * frame.Height];
        for (int pixel = 0, offset = 0; pixel < result.Length; pixel++, offset += 4)
        {
            if (frame.Bgra[offset + 3] == 0)
                result[pixel] = TransparentIndex;
            else
                result[pixel] = colors[frame.Bgra[offset + 2] << 16 | frame.Bgra[offset + 1] << 8 | frame.Bgra[offset]];
        }
        return result;
    }

    private static void WriteGraphicControlExtension(Stream output, ushort frameDelay)
    {
        output.Write([0x21, 0xf9, 0x04, 0x05]); // keep previous canvas + transparency
        WriteUInt16(output, frameDelay);
        output.WriteByte(TransparentIndex);
        output.WriteByte(0);
    }

    private static void WriteLoopExtension(Stream output) =>
        output.Write([0x21, 0xff, 0x0b, .. Encoding.ASCII.GetBytes("NETSCAPE2.0"), 0x03, 0x01, 0x00, 0x00, 0x00]);

    private static byte[] CompressLzw(byte[] input)
    {
        const int clearCode = 256;
        const int endCode = 257;
        List<byte> result = new();
        int accumulator = 0;
        int bits = 0;

        void WriteCode(int code)
        {
            accumulator |= code << bits;
            bits += 9;
            while (bits >= 8)
            {
                result.Add((byte)accumulator);
                accumulator >>= 8;
                bits -= 8;
            }
        }

        WriteCode(clearCode);
        int literalsSinceClear = 0;
        foreach (byte value in input)
        {
            WriteCode(value);
            literalsSinceClear++;
            // Keep the decoder's dictionary below the 10-bit transition.
            // This favors simple, reliable output over maximum compression.
            if (literalsSinceClear == 250)
            {
                WriteCode(clearCode);
                literalsSinceClear = 0;
            }
        }
        WriteCode(endCode);
        if (bits > 0)
            result.Add((byte)accumulator);
        return result.ToArray();
    }

    private static void WriteSubBlocks(Stream output, byte[] data)
    {
        for (int offset = 0; offset < data.Length; offset += 255)
        {
            int length = Math.Min(255, data.Length - offset);
            output.WriteByte((byte)length);
            output.Write(data, offset, length);
        }
        output.WriteByte(0);
    }

    private static void WriteUInt16(Stream output, int value)
    {
        output.WriteByte((byte)value);
        output.WriteByte((byte)(value >> 8));
    }
}
