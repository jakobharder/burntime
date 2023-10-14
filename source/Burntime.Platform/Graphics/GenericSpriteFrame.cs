using System;

namespace Burntime.Platform.Graphics;

public abstract class GenericSpriteFrame<TTexture> where TTexture : class
{
    public static TTexture? EmptyTexture { get; set; }

    public Vector2 Size { get; set; }
    public Vector2f Resolution { get; set; } = -1;

    public bool HasSystemCopy => _systemCopy is not null;
    protected byte[]? _systemCopy;

    /// <summary>
    /// Time stamp when the sprite was loaded.
    /// </summary>
    public long TimeStamp { get; protected set; }

    /// <summary>
    /// Texture is ready to use.
    /// </summary>
    public bool IsLoaded { get; protected set; }

#warning TODO public set access needed?
    public bool IsLoading { get; set; }

    public GenericSpriteFrame(TTexture texture, Vector2 size, byte[] systemCopy)
    {
        _texture = texture;
        Size = size;
        IsLoaded = texture != null;
        _systemCopy = systemCopy;
    }

    public GenericSpriteFrame()
    {
        _texture = null;
        Size = new(1, 1);
        IsLoaded = false;
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
}
