using Burntime.Platform.Resource;
using System;
using System.Diagnostics;

namespace Burntime.Platform.Graphics;

[DebuggerDisplay("Sprite = {id.ToString()}")]
public abstract class GenericSprite<TSpriteFrame, TTexture> : ISprite where TTexture : class where TSpriteFrame : GenericSpriteFrame<TTexture>
{
    public ResourceLoadType LoadType = ResourceLoadType.Now;
    protected IResourceManager resMan;
    protected bool colorKey = true;

#warning TODO access levels
    public TSpriteFrame[] internalFrames;
    public ResourceID id;

    protected GenericSprite()
    {
    }

    public bool IsNew { get; set; } = true;

    public override bool IsLoaded => (internalFrames != null && internalFrames[0].IsLoaded);

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
        if (IsLoaded) return true;
        
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
        internalFrames = new TSpriteFrame[1];
        internalFrames[0] = Frame;
        this.resMan = resMan;
    }

    public GenericSprite(IResourceManager resMan, String id, TSpriteFrame[] frames, SpriteAnimation animation)
    {
        this.id = id;
        internalFrames = frames;
        this.resMan = resMan;
        Animation = animation;
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
        if (IsLoaded || IsLoading)
            return;

        resMan.Reload(this, ResourceLoadType.Delayed);
    }
}
