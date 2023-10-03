using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;
using SlimDX.Direct3D9;

namespace Burntime.SlimDx.Graphics;

public class Sprite : GenericSprite<SpriteFrame, Texture>
{
    public Sprite(IResourceManager resourceManager, string id, SpriteFrame frame) : base(resourceManager, id, frame)
    {
    }

    public Sprite(IResourceManager resMan, string id, SpriteFrame[] frames, SpriteAnimation animation) : base(resMan, id, frames, animation)
    {
    }

    public override Sprite Clone()
    {
        return new(resMan, id, internalFrames, ani is null ? null : ani.Clone());
    }
}
