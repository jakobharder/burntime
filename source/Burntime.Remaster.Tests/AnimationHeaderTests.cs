using System.Collections.Generic;
using System.IO;
using Burntime.Data.BurnGfx;

namespace Burntime.Remaster.Tests;

using static Program;

static class AnimationHeaderTests
{
    public static IEnumerable<Case<int>> Cases()
    {
        yield return new("zero-terminated frame table", 2, () => Count(32, 8, 16, 0, 0));
        yield return new("full frame table without terminator", 2, () => Count(24, 4, 12));
        yield return new("backward offset is not a frame", 2, () => Count(32, 8, 16, 12, 0));
        yield return new("truncated frame header is not a frame", 1, () => Count(20, 8, 18, 0, 0));
        yield return new("portrait stale offset beyond EOF", 40, () => Portrait(14000, 300));
        yield return new("portrait stale offset inside earlier frame", 40, () => Portrait(25000, 600));
        yield return new("portrait valid 41st frame retained", 41, () => Portrait(18500, 400));
        yield return new("empty file", 0, () => Count(0));
        yield return new("oversized header", 0, () => Count(4, 256));
    }

    static int Portrait(int length, int stride)
    {
        ushort[] offsets = new ushort[128];
        for (int i = 0; i < 40; i++)
            offsets[i] = (ushort)(256 + i * stride);
        offsets[40] = 0x460d;
        return Count(length, offsets);
    }

    static int Count(int length, params ushort[] offsets)
    {
        using MemoryStream stream = new(new byte[length]);
        using BinaryWriter writer = new(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        foreach (ushort offset in offsets)
            writer.Write(offset);
        stream.Position = 0;
        RawImageFileReader reader = new(new Burntime.Platform.IO.File(stream, "test.ani"));
        reader.ReadHeader();
        // Re-reading must not depend on the previous stream position.
        reader.ReadHeader();
        return reader.ImageCount;
    }
}
