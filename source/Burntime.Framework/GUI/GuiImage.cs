using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Framework.GUI
{
    [DebuggerDisplay("GuiImage = {id.ToString()}")]
    public class GuiImage
    {
        ISprite sprite_;
        readonly List<(Vector2 Position, ISprite Sprite)> layers_ = new();
        string? description_;

        public int Width => sprite_.Width;
        public int Height => sprite_.Height;
        public Vector2 Size => sprite_.Size;
        public bool IsLoaded => sprite_.IsLoaded && layers_.All(layer => layer.Sprite.IsLoaded);
        public SpriteAnimation Animation => sprite_.Animation;

        public static implicit operator GuiImage(ResourceID id)
        {
            return new GuiImage(Module.Instance.ResourceManager.GetImage(id, ResourceLoadType.Delayed));
        }

        public static implicit operator GuiImage(string id)
        {
            return id.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? new GuiImage(id)
                : new GuiImage(Module.Instance.ResourceManager.GetImage(id, ResourceLoadType.Delayed));
        }

        public static implicit operator ISprite(GuiImage? image) => image?.sprite_;

        public GuiImage(ISprite sprite)
        {
            // clone the sprite to have own animation state
            sprite_ = sprite.Animation is null ? sprite : sprite.Clone();
        }

        GuiImage(string description)
        {
            description_ = description;
            Reload();
        }

        public void Reload()
        {
            if (description_ is null)
                return;

            ConfigFile config = new();
            config.Open(Module.Instance.ResourceManager.ResolveFileReplacement(description_));
            ConfigSection image = config["image"];
            bool hasSpriteScale = image.ContainsKey("sprite_scale");
            Vector2f spriteScale = image.GetVector2f("sprite_scale", Vector2f.One);
            Vector2f resolution = spriteScale.x != 0 && spriteScale.y != 0
                ? Vector2f.One / spriteScale
                : Vector2f.One;
            sprite_ = Module.Instance.ResourceManager.GetImage(
                image.GetString("background"), ResourceLoadType.Delayed);
            sprite_ = sprite_.Clone();
            if (hasSpriteScale)
                sprite_.Resolution = resolution;
            layers_.Clear();

            int animationCount = image.GetInt("animations");
            for (int i = 0; i < animationCount; i++)
            {
                ConfigSection settings = config["animation" + i];
                ISprite animation = Module.Instance.ResourceManager.GetImage(
                    settings.GetString("image"), ResourceLoadType.Delayed);
                animation = animation.Clone();
                if (hasSpriteScale)
                    animation.Resolution = resolution;

                if (settings.ContainsKey("speed"))
                    animation.Animation.Speed = settings.GetFloat("speed");
                if (settings.ContainsKey("interval_margin"))
                    animation.Animation.IntervalMargin = settings.GetInt("interval_margin");
                if (settings.ContainsKey("progressive"))
                    animation.Animation.Progressive = settings.GetBool("progressive");
                if (settings.ContainsKey("endless"))
                    animation.Animation.Endless = settings.GetBool("endless");
                if (settings.ContainsKey("delay"))
                    animation.Animation.Delay = settings.GetFloat("delay");
                if (settings.ContainsKey("reverse"))
                {
                    animation.Animation.ReverseAnimation = settings.GetBool("reverse");
                    if (animation.Animation.ReverseAnimation)
                        animation.Animation.GoLastFrame();
                }

                layers_.Add((settings.GetVector2("position"), animation));
            }
        }

        public GuiImage Clone()
        {
            GuiImage clone = new(sprite_);
            clone.description_ = description_;
            foreach (var layer in layers_)
                clone.layers_.Add((layer.Position, layer.Sprite.Animation is null
                    ? layer.Sprite
                    : layer.Sprite.Clone()));
            return clone;
        }

        public void Touch()
        {
            sprite_.Touch();
            foreach (var layer in layers_)
                layer.Sprite.Touch();
        }

        public void Update(float elapsed)
        {
            sprite_.Update(elapsed);
            foreach (var layer in layers_)
                layer.Sprite.Update(elapsed);
        }

        public void Draw(RenderTarget target) => Draw(target, Vector2.Zero);

        public void Draw(RenderTarget target, Vector2 position)
        {
            target.DrawSprite(position, sprite_);
            foreach (var layer in layers_)
                target.DrawSprite(position + layer.Position, layer.Sprite);
        }
    }
}
