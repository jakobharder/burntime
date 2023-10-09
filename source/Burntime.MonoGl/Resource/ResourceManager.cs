using System;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;
using Burntime.MonoGl;
using Burntime.MonoGl.Graphics;

namespace Burntime.Platform.Resource
{
    public class ResourceManager : ResourceManagerBase, IResourceManager, IDisposable
    {
        readonly BurntimeGame _engine;

        public ResourceManager(BurntimeGame engine) : base(engine)
        {
            _engine = engine;
            engine.ResourceManager = this;
        }

        #region Font
        public Font GetFont(String File, PixelColor ForeColor)
        {
            return GetFont(File, ForeColor, PixelColor.Black);
        }

        public Font GetFont(String File, PixelColor ForeColor, PixelColor BackColor)
        {
            ResourceInfoFont info;
            info.Name = File;
            if (BackColor != PixelColor.Black)
            {
                info.Fore = ForeColor;
                info.Back = BackColor;
            }
            else
            {
                info.Fore = PixelColor.White;
                info.Back = PixelColor.Black;
            }

            Font font = new Font();
            font.Info = new FontInfo();
            font.Info.Font = File.ToLower();
            font.Info.ForeColor = ForeColor;
            font.Info.BackColor = BackColor;
            font.Info.UseBackColor = !(BackColor.a == 255 && BackColor.r == 0 && BackColor.g == 0 && BackColor.b == 0);

            if (fonts.ContainsKey(info))
            {
                font.charInfo = fonts[info].charInfo;
                font.sprite = fonts[info].sprite;
                font.offset = fonts[info].offset;
                font.height = fonts[info].height;
                return font;
            }

            FilePath path = File;
            if (!fontProcessors.ContainsKey(path.Extension))
                return null;

            _engine.IncreaseLoadingCount();

            IFontProcessor processor = (IFontProcessor)Activator.CreateInstance(fontProcessors[path.Extension].GetType());
            processor.Process((string)path);
            Log.Debug("load \"" + path.FullPath + "\"");

            processor.Color = ForeColor;
            processor.Shadow = BackColor;

            SpriteFrame frame = new();
            lock (sprites)
                MemoryUsage += frame.LoadFromProcessor(processor, keepSystemCopy: true);

            font.sprite = new Sprite(this, "", frame);
            font.sprite.Resolution = processor.Factor;

            font.charInfo = processor.CharInfo;
            font.offset = processor.Offset;
            font.height = processor.Size.y;

            _engine.DecreaseLoadingCount();

            lock (fonts)
                fonts.Add(info, font);
            return font;
        }
        #endregion

        #region Sprites
        public ISprite GetImage(ResourceID id, ResourceLoadType loadType = ResourceLoadType.Delayed)
        {
            return GetSprite(id, loadType);
        }

        ISprite GetSprite(ResourceID id, ResourceLoadType LoadType)
        {
            Vector2f factor = Vector2f.One;
            ResourceID realid = (string)id;

            var replaced = GetReplacement(id);
            if (replaced is not null)
            {
                // if available return newid sprite right away
                if (sprites.TryGetValue(replaced.Id, out ISprite value))
                {
                    return value.Clone();
                }
                // otherwise check if newid is available, if not fallback to old id
                else if (CheckReplacementID(replaced.Id))
                {
                    id = replaced.Id;
                    factor = replaced.Factor;
                }
            }

            if (sprites.ContainsKey(id))
            {
                return sprites[id].Clone();
            }
            else
            {
                Sprite s;

                String format = id.Format;

                if (Log.DebugOut)
                {
                    string info = $"init \"{id}\"";
                    if (factor != Vector2f.One)
                        info += $" {System.Math.Round(factor.x * 100)}% x {System.Math.Round(factor.y * 100)}%";
                    if (realid != id)
                        info += $" ({realid})";
                    Log.Debug(info);
                }

                if (!spriteProcessors.ContainsKey(format))
                    return null;

                _engine.IncreaseLoadingCount();

                lock (this)
                {
                    ISpriteProcessor loader = GetSpriteProcessor(id, false);
                    if (loader is ISpriteAnimationProcessor)
                    {
                        ISpriteAnimationProcessor loaderAni = (ISpriteAnimationProcessor)loader;

                        loader.Process(id);

                        int count = loaderAni.FrameCount;

                        SpriteFrame[] frames = new SpriteFrame[loaderAni.FrameCount];
                        for (int i = 0; i < loaderAni.FrameCount; i++)
                        {
                            frames[i] = new SpriteFrame();
                        }

                        s = new Sprite(this, realid, frames, new SpriteAnimation(loaderAni.FrameCount));
                        s.LoadType = LoadType;

                        sprites.Add(id, s);
                    }
                    else
                    {
                        SpriteFrame si = new SpriteFrame();
                        s = new Sprite(this, realid, si);
                        s.LoadType = LoadType;

                        sprites.Add(id, s);
                    }
                }

                if (LoadType == ResourceLoadType.Now)
                    Reload(s, ResourceLoadType.Now);

                _engine.DecreaseLoadingCount();
                return s;
            }
        }

        internal void Reload(Sprite Sprite, ResourceLoadType LoadType)
        {
            Sprite.internalFrames[0].IsLoading = true;

            if (LoadType == ResourceLoadType.Delayed)
            {
                delayLoader.Enqueue(Sprite);
                return;
            }

            // System copied sprites like fonts
            //for (int i = 0; i < Sprite.internalFrames.Length; i++)
            //{
            //    if (Sprite.internalFrames[i].HasSystemCopy)
            //    {
            //        // nothing to do
            //    }
            //}

            ResourceID id = Sprite.ID;
            if (id.File == "" || !spriteProcessors.ContainsKey(id.Format))
                return;

            ResourceID realid = (string)id;

            // check replacements
            Sprite.internalFrames[0].Resolution = 1;
            if (replacement != null)
            {
                var replaced = GetReplacement(id);
                if (replaced is not null)
                {
                    if (CheckReplacementID(replaced.Id))
                    {
                        id = replaced.Id;
                        Sprite.internalFrames[0].Resolution = replaced.Factor;
                    }
                }
            }

            _engine.IncreaseLoadingCount();

            if (Log.DebugOut)
            {
                string info = Sprite.IsNew ? "load" : "reload";
                info += $" \"{id}\"";
                if (Sprite.internalFrames[0].Resolution != 1)
                    info += $" " + Log.FormatPercentage(Sprite.internalFrames[0].Resolution);
                if (realid.ToString() != id.ToString())
                    info += $" ({realid})";
                Log.Debug(info);
            }
            Sprite.IsNew = false;
            ISpriteProcessor loader = GetSpriteProcessor(id, true);
            loader.Process(id);

            if (loader is ISpriteAnimationProcessor loaderAni)
            {
                if (Sprite.internalFrames.Length != loaderAni.FrameCount)
                    Array.Resize(ref Sprite.internalFrames, loaderAni.FrameCount);
                Sprite.Animation ??= new SpriteAnimation(loaderAni.FrameCount);
                Sprite.Animation.Frame = System.Math.Min(loaderAni.FrameCount - 1, Sprite.Animation.Frame);
                Sprite.Animation.FrameCount = loaderAni.FrameCount;

                for (int i = 0; i < loaderAni.FrameCount; i++)
                {
                    if (!loaderAni.SetFrame(i))
                    {
                        Array.Resize(ref Sprite.internalFrames, loaderAni.FrameCount);
                        Sprite.Animation.FrameCount = i;
                        break;
                    }

                    Sprite.internalFrames[i] = Sprite.internalFrames[i] ?? new SpriteFrame();

                    lock (sprites)
                        MemoryUsage += Sprite.internalFrames[i].LoadFromProcessor(loader);
                    Sprite.internalFrames[i].Resolution = Sprite.internalFrames[0].Resolution;
                }
            }
            else
            {
                if (Sprite.Animation is not null)
                {
                    Sprite.Animation.Frame = 0;
                    Sprite.Animation.FrameCount = 1;
                }
                lock (sprites)
                    MemoryUsage += Sprite.internalFrames[0].LoadFromProcessor(loader);
            }

            _engine.DecreaseLoadingCount();
        }
        #endregion

        ISprite IResourceManager.GetImage(ResourceID id, ResourceLoadType loadType) => GetImage(id, loadType);
        void IResourceManager.Reload(ISprite sprite, ResourceLoadType loadType) => Reload(sprite as Sprite, loadType);
    }
}
