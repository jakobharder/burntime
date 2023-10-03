using Burntime.Classic.Logic;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Burntime.MonoGl.Graphics;

public class SpriteFrame : Platform.Graphics.GenericSpriteFrame<Texture2D>
{
    public SpriteFrame()
    {
    }

    public SpriteFrame(Texture2D texture, Vector2 size, byte[] systemCopy) : base(texture, size, systemCopy)
    {
    }

    public int LoadFromProcessor(ISpriteProcessor loader, RenderDevice renderDevice, bool keepSystemCopy = false)
    {
        var tex = renderDevice.CreateTexture(MakePowerOfTwo(loader.Size.x), MakePowerOfTwo(loader.Size.y));
        int usedMemory = tex.Width * tex.Height * 4;

        byte[] buffer = new byte[usedMemory];
        using MemoryStream stream = new(buffer);
        loader.Render(stream, tex.Width * 4);

#warning TODO how to avoid ARGB -> ABGR?
        for (int y = 0; y < tex.Height; y++)
        {
            for (int x = 0; x < tex.Width; x++)
            {
                byte t = 0;
                t = buffer[(y * tex.Width + x) * 4 + 0];
                buffer[(y * tex.Width + x) * 4 + 0] = buffer[(y * tex.Width + x) * 4 + 2];
                buffer[(y * tex.Width + x) * 4 + 2] = t;
            }
        }

        
        if (keepSystemCopy)
            _systemCopy = buffer.ToArray();
        
        tex.SetData(buffer);

        _texture = tex;

        if (loader is ISpriteAnimationProcessor loaderAni)
            Size = loaderAni.FrameSize;
        else
            Size = loader.Size;

        TimeStamp = System.Diagnostics.Stopwatch.GetTimestamp();
        loading = false;
        loaded = true;
        return usedMemory;
    }

    protected static int MakePowerOfTwo(int nValue)
    {
        nValue--;
        int i;
        for (i = 0; nValue != 0; i++)
            nValue >>= 1;
        return 1 << i;
    }

    protected override bool IsDisposed => _texture?.IsDisposed ?? true;

    public override int Unload()
    {
        if (_texture is null || _texture.IsDisposed) return 0;

        int freedMemory = 0;
#warning TODO count freed memory
        //try
        //{
        //    SlimDX.Direct3D9.SurfaceDescription desc = _texture.GetLevelDescription(0);
        //    freedMemory = desc.Width * desc.Height * 4;
        //}
        //catch (Exception)
        //{
        //    // TODO make cleaner
        //}

        _texture.Dispose();
        _texture = null;
        loaded = false;
        loading = false;

        return freedMemory;
    }

    internal void RestoreFromSystemCopy()
    {
#warning TODO restore system copy for fonts
        //SlimDX.DataRectangle data = _texture.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
        //data.Data.Write(systemCopy, 0, systemCopy.Length);
        //_texture.UnlockRectangle(0);
    }
}
