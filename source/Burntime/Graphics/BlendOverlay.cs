using SlimDX.Direct3D9;

namespace Burntime.Platform.Graphics;

public class BlendOverlay : BlendOverlayBase
{
    Texture emptyTexture;

    public BlendOverlay(Vector2 screenSize) : base(screenSize)
    {
    }

    public void Render(GameTime RenderTime, SlimDX.Direct3D9.Sprite SpriteRenderer)
    {
        if (!(BlockFadeOut && _blendFade.IsFadingOut))
            _blendFade.Update(RenderTime.Elapsed);

        if (!_blendFade.IsOut)
        {
            SpriteRenderer.Transform = SlimDX.Matrix.Identity;
            System.Drawing.Rectangle rc = new System.Drawing.Rectangle(0, 0, _size.x, _size.y);
            SpriteRenderer.Draw(emptyTexture, rc, new SlimDX.Vector3(0, 0, 0), new SlimDX.Vector3(0, 0, 0), new SlimDX.Color4(_blendFade.State, 0, 0, 0));
        }
    }

    public void Load(Device device)
    {
        emptyTexture = new Texture(device, 1, 1, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
        SlimDX.DataRectangle dr = emptyTexture.LockRectangle(0, LockFlags.Discard);
        dr.Data.Write<uint>(0xffffffff);
        emptyTexture.UnlockRectangle(0);
    }

    public void Unload()
    {
        emptyTexture.Dispose();
    }
}
