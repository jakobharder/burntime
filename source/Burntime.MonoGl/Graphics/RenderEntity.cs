using Burntime.Platform.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Burntime.MonoGl.Graphics;

class SpriteEntity : RenderEntity
{
    public Texture2D Texture;
    public SpriteFrame SpriteFrame;
    public Rectangle Rectangle;
    public Vector3 Position;
    public Color Color;
    public float Factor = 1;
}

class LineEntity : RenderEntity
{
    public Vector3 Start;
    public Vector3 End;
    public Color Color;
}
