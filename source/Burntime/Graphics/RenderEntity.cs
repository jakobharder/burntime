using System.Drawing;
using SlimDX.Direct3D9;

namespace Burntime.Platform.Graphics;

class SpriteEntity : RenderEntity
{
    public Texture Texture;
    public Rectangle Rectangle;
    public SlimDX.Vector3 Position;
    public SlimDX.Color4 Color;
    public float Factor = 1;
}

class LineEntity : RenderEntity
{
    public SlimDX.Vector3 Start;
    public SlimDX.Vector3 End;
    public SlimDX.Color4 Color;
}
