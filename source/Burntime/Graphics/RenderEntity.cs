using System;
using System.Collections.Generic;
using System.Text;

using System.Drawing;
using SlimDX.Direct3D9;

namespace Burntime.Platform.Graphics
{
    class RenderEntity
    {
    }

    class SpriteEntity : RenderEntity
    {
        public Texture Texture;         // texture
        public Rectangle Rectangle;     // source rectangle
        public SlimDX.Vector3 Position; // position
        public SlimDX.Color4 Color;     // color fill
        public float Factor = 1;        // texture resolution relative to game resolution
    }

    class LineEntity : RenderEntity
    {
        public SlimDX.Vector3 Start;
        public SlimDX.Vector3 End;
        public SlimDX.Color4 Color;
    }
}
