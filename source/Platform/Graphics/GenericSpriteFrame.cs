using System;

namespace Burntime.Platform.Graphics;

public abstract class GenericSpriteFrame<TTexture> where TTexture : class
{
    public static TTexture? EmptyTexture { get; set; }

    public Vector2 Size { get; set; }
    public float Resolution { get; set; } = -1;

    public bool HasSystemCopy => _systemCopy is not null;
    protected byte[]? _systemCopy;

    public GenericSpriteFrame(TTexture texture, Vector2 size, byte[] systemCopy)
    {
        _texture = texture;
        Size = size;
        loaded = texture != null;
        _systemCopy = systemCopy;
    }

    public GenericSpriteFrame()
    {
        _texture = null;
        Size = new(1, 1);
        loaded = false;
    }

    public TTexture Texture
    {
        get 
        {
            if (_texture != null && IsDisposed)
                _texture = null;
            return _texture ?? EmptyTexture!; 
        }
        set => _texture = value;
    }
    protected TTexture? _texture;

    protected abstract bool IsDisposed { get; }
    public abstract int Unload();

    public long TimeStamp;

#warning TODO rework access
    public bool loaded;
    public bool loading;
}
