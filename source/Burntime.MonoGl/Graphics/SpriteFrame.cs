using Burntime.Classic.Logic;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Burntime.MonoGl.Graphics;

public class SpriteFrame : Platform.Graphics.GenericSpriteFrame<Texture2D>
{
    int _usedMemory;
    Vector2 _textureSize;

    public SpriteFrame()
    {
    }

    public SpriteFrame(Texture2D texture, Vector2 size, byte[] systemCopy) : base(texture, size, systemCopy)
    {
    }

    public int LoadFromProcessor(ISpriteProcessor loader, RenderDevice renderDevice, bool keepSystemCopy = false)
    {
        const int PIXEL_BYTES = 4;

        _textureSize = new(MakePowerOfTwo(loader.Size.x), MakePowerOfTwo(loader.Size.y));
        _usedMemory = _textureSize.Count * PIXEL_BYTES;

        _systemCopy = new byte[_usedMemory];
        using MemoryStream stream = new(_systemCopy);
        loader.Render(stream, _textureSize.x * PIXEL_BYTES);
        stream.Dispose();

#warning TODO how to avoid ARGB -> ABGR?
        for (int y = 0; y < _textureSize.y; y++)
        {
            for (int x = 0; x < _textureSize.x; x++)
            {
                (_systemCopy[(y * _textureSize.x + x) * PIXEL_BYTES + 2], _systemCopy[(y * _textureSize.x + x) * PIXEL_BYTES + 0]) =
                    (_systemCopy[(y * _textureSize.x + x) * PIXEL_BYTES + 0], _systemCopy[(y * _textureSize.x + x) * PIXEL_BYTES + 2]);
            }
        }

        //var tex = renderDevice.CreateTexture(adjustedSize.x, adjustedSize.y);
        //tex.SetData(_systemCopy);

        //if (!keepSystemCopy)
        //    _systemCopy = null;

        //_texture = tex;
        Size = loader.Size;

        TimeStamp = Stopwatch.GetTimestamp();
        IsLoading = false;
        IsLoaded = true;
        return _usedMemory;
    }

    public void CreateTexture(RenderDevice renderDevice)
    {
        if (_texture is not null) return;

        var tex = renderDevice.CreateTexture(_textureSize.x, _textureSize.y);
        tex.SetData(_systemCopy);

        //if (!keepSystemCopy)
        //    _systemCopy = null;

        _texture = tex;
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
        IsLoaded = false;
        IsLoading = false;

#warning TODO slimdx, unload systemCopy?

        if (_texture is null || _texture.IsDisposed) return 0;

        _texture.Dispose();
        _texture = null;

        return _usedMemory;
    }

    internal void RestoreFromSystemCopy()
    {
#warning TODO restore system copy for fonts
        //SlimDX.DataRectangle data = _texture.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
        //data.Data.Write(systemCopy, 0, systemCopy.Length);
        //_texture.UnlockRectangle(0);
    }
}
