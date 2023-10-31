namespace Burntime.Platform.Graphics;

public class RenderTarget
{
    public Vector2 Offset { get; set; }
    public float Layer
    {
        get => _engine.Layer;
        set => _engine.Layer = value;
    }

    public int Width => _rc.Width;
    public int Height => _rc.Height;
    public Vector2 ScreenOffset => _rc.Position;
    public Vector2 ScreenSize => _engine.MainTarget.Size;
    public Vector2 Size => _rc.Size;

#warning move set to engine/renderdevice
    public float Elapsed { get; set; }

    public RenderTarget(IEngine engine, Rect rc)
    {
        _engine = engine;
        _rc = rc;
    }

    #region DrawSprite methods
    public void DrawSprite(ISprite sprite)
    {
        if (sprite == null)
            return;

        DrawSprite(Vector2.Zero, sprite);
    }

    public void DrawSprite(Vector2 pos, ISprite sprite)
    {
        if (sprite == null)
            return;

        _engine.RenderSprite(sprite, pos + _rc.Position + Offset);
    }

    public void DrawSprite(Vector2 pos, ISprite sprite, float alpha)
    {
        if (sprite == null)
            return;

        _engine.RenderSprite(sprite, pos + _rc.Position + Offset, alpha);
    }

    public void DrawSprite(Vector2 pos, ISprite sprite, Rect srcRect)
    {
        if (sprite == null)
            return;

        _engine.RenderSprite(sprite, pos + _rc.Position + Offset);
    }

    ISprite? _selectedSprite;
    public void SelectSprite(ISprite sprite)
    {
        _selectedSprite = sprite;
    }

    public void DrawSelectedSprite(Vector2 pos, Rect srcRect, PixelColor color)
    {
        if (_selectedSprite is null) return;

        _engine.RenderSprite(_selectedSprite, pos + _rc.Position + Offset, srcRect.Position, srcRect.Width, srcRect.Height, color);
    }
    #endregion

    public void RenderRect(Vector2 pos, Vector2 size, PixelColor color)
    {
        _engine.RenderRect(pos + _rc.Position + Offset, size, color);
    }

    public void RenderLine(Vector2 start, Vector2 end, PixelColor color)
    {
        _engine.RenderLine(start + _rc.Position + Offset, end + _rc.Position + Offset, color);
    }

    public RenderTarget GetSubBuffer(Rect rc)
    {
        RenderTarget target = new(_engine, new Rect(_rc.Position + rc.Position, rc.Size))
        {
            Elapsed = Elapsed,
            Offset = Offset
        };
        return target;
    }

    readonly IEngine _engine;
    readonly Rect _rc;
}
