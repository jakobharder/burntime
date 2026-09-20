using System;
using System.IO;
using System.Collections.Generic;

using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource
{
    public readonly record struct FontSpriteInfo(
        string Image,
        Vector2 FrameSize,
        int StartFrame,
        int FrameCount,
        int Advance);

    public interface IFontProcessor : ISpriteProcessor
    {
        Dictionary<char, CharInfo> CharInfo { get; }
        Dictionary<string, float> Kerning { get; }
        IReadOnlyDictionary<char, FontSpriteInfo> Indicators { get; }
        IReadOnlyDictionary<char, FontSpriteInfo> Icons { get; }
        int Offset { get; }
        int GlyphHeight { get; }
        Vector2f Factor { get; }
        bool PostFilter { get; }

        PixelColor Color { get; set; }
        PixelColor Shadow { get; set; }
    }
}
