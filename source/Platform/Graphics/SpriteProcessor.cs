using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;

namespace Burntime.Platform.Resource
{
    public interface ISpriteProcessor
    {
        void Process(ResourceID id);
        Vector2 Size { get; }
        void Render(Stream s, int stride);
    }

    public interface ISpriteAnimationProcessor
    {
        int FrameCount { get; }
        Vector2 FrameSize { get; }
        bool SetFrame(int frame);
    }
}
