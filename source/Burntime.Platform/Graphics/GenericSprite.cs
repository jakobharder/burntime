using Burntime.Platform.Resource;
using System;
using System.Diagnostics;

namespace Burntime.Platform.Graphics;

[DebuggerDisplay("Sprite = {id.ToString()}")]
public abstract class GenericSprite<TSpriteFrame, TTexture> : ISprite where TTexture : class where TSpriteFrame : GenericSpriteFrame<TTexture>
{
    sealed class FrameStorage
    {
        public TSpriteFrame[] Frames;

        public FrameStorage(TSpriteFrame[] frames)
        {
            Frames = frames;
        }
    }

    public ResourceLoadType LoadType = ResourceLoadType.Now;
    protected IResourceManager resMan;
    protected bool colorKey = true;
    // Clones have independent playback state, but a reload must replace the
    // resource frames for every clone that refers to the sprite.
    FrameStorage frameStorage = new([]);

#warning TODO access levels
    public TSpriteFrame[] internalFrames
    {
        get => frameStorage.Frames;
        set => frameStorage.Frames = value;
    }
    public ResourceID id;

    protected GenericSprite()
    {
    }

    protected GenericSprite(GenericSprite<TSpriteFrame, TTexture> source)
    {
        id = source.id;
        frameStorage = source.frameStorage;
        resMan = source.resMan;
        LoadType = source.LoadType;
        colorKey = source.colorKey;
        IsNew = source.IsNew;
        Animation = source.Animation?.Clone();
    }

    public bool IsNew { get; set; } = true;

    public override bool IsLoaded => (internalFrames != null && internalFrames[0].IsLoaded);
    public override bool HasSystemCopy =>
        internalFrames != null && internalFrames.Length > 0 &&
        internalFrames[0].HasSystemCopy;

    public bool IsLoading
    {
        get { return (internalFrames != null && internalFrames[0].IsLoading); }
    }

#warning TODO resolution down and up again for newgfx may lead to precision loss
    public override Vector2 Size => (Vector2)((Vector2f)Frame.Size * Frame.Resolution);
    public Vector2 OriginalSize => Frame.Size;

    public int CurrentFrame
    {
        get { if (Animation != null) return Animation.Frame; return 0; }
    }

    public bool ColorKey
    {
        get { return colorKey; }
        set { colorKey = value; }
    }

    public override ResourceID ID
    {
        get { return id; }
    }

    public override SpriteAnimation Animation { get; set; }
    public override Vector2f Resolution
    {
        get => internalFrames[0].Resolution;
        set => internalFrames[0].Resolution = value;
    }

    public abstract override ISprite Clone();

    public override void Update(float elapsed)
    {
        if (Animation != null && internalFrames != null)
            Animation.Update(elapsed);
    }

    public override bool Touch()
    {
        if (IsLoaded || HasSystemCopy) return true;
        
        Load();
        return false;
    }

    // internal access
    public TSpriteFrame Frame
    {
        get 
        { 
            Load();
            if (CurrentFrame >= internalFrames.Length)
                return internalFrames[0];
            return internalFrames[CurrentFrame]; 
        }
    }

    public TSpriteFrame[] Frames
    {
        get { Load(); return internalFrames; }
    }

    public GenericSprite(IResourceManager resMan, String ID, TSpriteFrame Frame)
    {
        id = ID;
        frameStorage = new([Frame]);
        this.resMan = resMan;
    }

    public GenericSprite(IResourceManager resMan, String id, TSpriteFrame[] frames, SpriteAnimation animation)
    {
        this.id = id;
        frameStorage = new(frames);
        this.resMan = resMan;
        Animation = animation;
    }

    public void ResizeFrames(int frameCount)
    {
        Array.Resize(ref frameStorage.Frames, frameCount);
    }

    public override int Unload()
    {
        int freedMemory = 0;

        foreach (var frame in internalFrames)
            freedMemory += frame.Unload();

        return freedMemory;
    }

    public void Load()
    {
        if (IsLoaded || HasSystemCopy || IsLoading)
            return;

        resMan.Reload(this, ResourceLoadType.Delayed);
    }
}
