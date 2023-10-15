using Burntime.Platform.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Burntime.MonoGame.Graphics;

class SpriteEntity : RenderEntity
{
    public Texture2D Texture;
    public SpriteFrame SpriteFrame;
    public Rectangle Rectangle;
    public Vector3 Position;
    public Color Color;
    public Platform.Vector2f Factor = Platform.Vector2f.One;
}

class LineEntity : RenderEntity
{
    public Vector3 Start;
    public Vector3 End;
    public Color Color;
}
