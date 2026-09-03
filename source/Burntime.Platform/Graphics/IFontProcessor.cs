using System;
using System.IO;
using System.Collections.Generic;

using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource
{
    public interface IFontProcessor : ISpriteProcessor
    {
        Dictionary<char, CharInfo> CharInfo { get; }
        Dictionary<string, int> Kerning { get; }
        int Offset { get; }
        int GlyphHeight { get; }
        Vector2f Factor { get; }
        bool PostFilter { get; }

        PixelColor Color { get; set; }
        PixelColor Shadow { get; set; }
    }
}
