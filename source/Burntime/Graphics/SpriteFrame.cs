using Burntime.Platform;
using Burntime.Platform.Resource;
using SlimDX.Direct3D9;
using System;

namespace Burntime.SlimDx.Graphics;

public class SpriteFrame : Platform.Graphics.GenericSpriteFrame<Texture>
{
    public SpriteFrame()
    {
    }

    public SpriteFrame(Texture texture, Vector2 size, byte[] systemCopy) : base(texture, size, systemCopy)
    {
    }

    protected override bool IsDisposed => _texture?.Disposed ?? true;

    public override int Unload()
    {
        if (_texture is null || _texture.Disposed) return 0;

        int freedMemory = 0;
        try
        {
            SlimDX.Direct3D9.SurfaceDescription desc = _texture.GetLevelDescription(0);
            freedMemory = desc.Width * desc.Height * 4;
        }
        catch (Exception)
        {
            // TODO make cleaner
        }

        _texture.Dispose();
        _texture = null;
        loaded = false;
        loading = false;

        return freedMemory;
    }

    internal void RestoreFromSystemCopy()
    {
        SlimDX.DataRectangle data = _texture.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
        data.Data.Write(_systemCopy, 0, _systemCopy.Length);
        _texture.UnlockRectangle(0);
    }
}
