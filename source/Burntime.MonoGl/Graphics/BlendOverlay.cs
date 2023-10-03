using Burntime.Platform.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Burntime.MonoGl.Graphics;

public class BlendOverlay : BlendOverlayBase
{
    Texture2D _emptyTexture;

    public BlendOverlay(Platform.Vector2 screenSize) : base(screenSize)
    {
    }

    public void Render(float elapsedSeconds, SpriteBatch spriteBatch)
    {
        if (!(BlockFadeOut && _blendFade.IsFadingOut))
            _blendFade.Update(elapsedSeconds);

        if (!_blendFade.IsOut)
        {
            spriteBatch.Draw(_emptyTexture, 
                position: Vector2.Zero,
                sourceRectangle: new Rectangle(0, 0, _size.x, _size.y), 
                new Color(0, 0, 0, _blendFade.State), 
                rotation: 0,
                origin: Vector2.Zero,
                scale: Vector2.One,
                SpriteEffects.None,
                layerDepth: 0.0f);
        }
    }

    public void Load(GraphicsDevice device)
    {
        _emptyTexture = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
        _emptyTexture.SetData(new Color[] { Color.White });
    }

    public void Unload()
    {
        _emptyTexture.Dispose();
        _emptyTexture = null;
    }
}
