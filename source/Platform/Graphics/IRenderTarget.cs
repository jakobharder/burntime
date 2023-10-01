namespace Burntime.Platform.Graphics;

public interface IRenderTarget
{
    int Layer { get; set; }
    Vector2 Size { get; }
    Vector2 Offset { get; set; }
    int Width { get; }
    int Height { get; }

    Vector2 ScreenOffset { get; }
    Vector2 ScreenSize { get; }

    float Elapsed { get; }

    void SelectSprite(ISprite sprite);
    void DrawSelectedSprite(Vector2 pos, Rect srcRect);
    void DrawSelectedSprite(Vector2 pos, Rect srcRect, PixelColor color);

    void DrawSprite(ISprite sprite);
    void DrawSprite(Vector2 pos, ISprite sprite);
    void DrawSprite(Vector2 pos, ISprite sprite, float alpha);
    void DrawSprite(Vector2 pos, ISprite sprite, Rect srcRect);

    void RenderRect(Vector2 pos, Vector2 size, PixelColor color);
    void RenderLine(Vector2 start, Vector2 end, PixelColor color);

    IRenderTarget GetSubBuffer(Rect rc);
}
