using System;
using System.Diagnostics;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Framework.GUI
{
    [DebuggerDisplay("GuiImage = {id.ToString()}")]
    public class GuiImage
    {
        readonly ISprite sprite_;

        public int Width => sprite_.Width;
        public int Height => sprite_.Height;
        public bool IsLoaded => sprite_.IsLoaded;
        public SpriteAnimation Animation => sprite_.Animation;

        public static implicit operator GuiImage(ResourceID id)
        {
            return new GuiImage(Module.Instance.ResourceManager.GetImage(id, ResourceLoadType.Delayed));
        }

        public static implicit operator GuiImage(string id)
        {
            return new GuiImage(Module.Instance.ResourceManager.GetImage(id, ResourceLoadType.Delayed));
        }

        public static implicit operator ISprite(GuiImage image) => image?.sprite_;

        public GuiImage(ISprite sprite)
        {
            // clone the sprite to have own animation state
            sprite_ = sprite.Animation is null ? sprite : sprite.Clone();
        }

        public void Touch() => sprite_.Touch();
        public void Update(float elapsed) => sprite_.Update(elapsed);
    }
}
