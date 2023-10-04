using Burntime.Platform.Resource;

namespace Burntime.Platform.Graphics;

public abstract class ISprite : DataObject
{
    public abstract ResourceID ID { get; }

    public abstract Vector2f Resolution { get; set; }
    public abstract Vector2 Size { get; }
    public abstract SpriteAnimation Animation { get; }
    public abstract bool IsLoaded { get; }

    public int Width => Size.x;
    public int Height => Size.y;

    /// <summary>
    /// Trigger texture loading.
    /// </summary>
    /// <returns>true when already loaded</returns>
    public abstract bool Touch();
    public abstract void Update(float elapsed);

    /// <summary>
    /// Free all texture memory.
    /// </summary>
    /// <returns>freed memory in bytes</returns>
    public abstract int Unload();

    public abstract ISprite Clone();
}
