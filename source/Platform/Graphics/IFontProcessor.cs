using System;
using System.IO;
using System.Collections.Generic;

using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource
{
    public interface IFontProcessor
    {
        void Process(ResourceID ID);
        Vector2 Size { get; }
        void Render(Stream Stream, int Stride, PixelColor Fore, PixelColor Back);
        Dictionary<char, CharInfo> CharInfo { get; }
        int Offset { get; }
        float Factor { get; }
    }
}
