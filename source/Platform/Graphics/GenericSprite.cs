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
    public SpriteAnimation ani;

    protected GenericSprite()
    {
    }

    public bool IsNew { get; set; } = true;

    public override bool IsLoaded => (internalFrames != null && internalFrames[0].loaded);

    public bool IsLoading
    {
        get { return (internalFrames != null && internalFrames[0].loading); }
    }

    public override Vector2 Size => Frame.Size * Frame.Resolution;
    public Vector2 OriginalSize => Frame.Size;

    public int CurrentFrame
    {
        get { if (ani != null) return ani.Frame; return 0; }
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

    public override SpriteAnimation Animation => ani;
    public override float Resolution
    {
        get => internalFrames[0].Resolution;
        set => internalFrames[0].Resolution = value;
    }

    public abstract override ISprite Clone();

    public override void Update(float elapsed)
    {
        if (ani != null && internalFrames != null)
            ani.Update(elapsed);
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
        ani = animation;
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
