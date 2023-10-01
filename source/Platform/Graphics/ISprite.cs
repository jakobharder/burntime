using Burntime.Platform.Resource;

namespace Burntime.Platform.Graphics;

public abstract class ISprite : DataObject
{
    public abstract ResourceID ID { get; }

    public abstract float Resolution { get; set; }
    public abstract Vector2 Size { get; }
    public abstract SpriteAnimation Animation { get; }
    public abstract bool IsLoaded { get; }

    public int Width => Size.x;
    public int Height => Size.y;

    public abstract void Touch();
    public abstract void Update(float elapsed);
    public abstract void Unload();

    public abstract ISprite Clone();
}
