using System;
using System.IO;
using System.Collections.Generic;

using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource
{
    public interface IFontProcessor : ISpriteProcessor
    {
        Dictionary<char, CharInfo> CharInfo { get; }
        int Offset { get; }
        float Factor { get; }

        PixelColor Color { get; set; }
        PixelColor Shadow { get; set; }
    }
}
