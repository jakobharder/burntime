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
            MemoryUsage += frame.LoadFromProcessor(processor, keepSystemCopy: true);

            font.sprite = new Sprite(this, "", frame);
            font.sprite.Resolution = processor.Factor;

            font.charInfo = processor.CharInfo;
            font.offset = processor.Offset;
            font.height = processor.Size.y;

            _engine.DecreaseLoadingCount();

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
            float factor = 1;
            ResourceID realid = (string)id;

            // check replacements
            if (replacement != null)
            {
                ResourceID newid = GetReplacementID(id);
                if (newid != null)
                {
                    // if available return newid sprite right away
                    if (sprites.TryGetValue(newid, out ISprite value))
                    {
                        return value.Clone();
                    }
                    // otherwise check if newid is available, if not fallback to old id
                    else if (CheckReplacementID(newid))
                    {
                        id = newid;
                        factor = replacement[""].GetFloat("sprite_resolution");
                    }
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
                    if (factor != 1)
                        info += $" {System.Math.Round(factor * 100)}%";
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

            for (int i = 0; i < Sprite.internalFrames.Length; i++)
            {
                //if (Sprite.internalFrames[i].HasSystemCopy)
                //{
                //    SlimDX.Direct3D9.Texture tex = _engine.Device.CreateTexture(MakePowerOfTwo(Sprite.internalFrames[i].Size.x), MakePowerOfTwo(Sprite.internalFrames[i].Size.y));
                //    SlimDX.Direct3D9.SurfaceDescription desc = tex.GetLevelDescription(0);
                //    MemoryUsage += desc.Width * desc.Height * 4;

                //    Sprite.internalFrames[i].texture = tex;
                //    Sprite.internalFrames[i].RestoreFromSystemCopy();
                //    Sprite.internalFrames[i].loading = false;
                //    Sprite.internalFrames[i].loaded = true;
                //}
            }

            ResourceID id = Sprite.ID;
            if (id.File == "" || !spriteProcessors.ContainsKey(id.Format))
                return;

            ResourceID realid = (string)id;

            // check replacements
            Sprite.internalFrames[0].Resolution = 1;
            if (replacement != null)
            {
                ResourceID newid = GetReplacementID(id);
                if (newid != null)
                {
                    if (CheckReplacementID(newid))
                    {
                        id = newid;
                        Sprite.internalFrames[0].Resolution = replacement[""].GetFloat("sprite_resolution");
                    }
                }
            }

            _engine.IncreaseLoadingCount();

            if (Log.DebugOut)
            {
                string info = Sprite.IsNew ? "load" : "reload";
                info += $" \"{id}\"";
                if (Sprite.internalFrames[0].Resolution != 1)
                    info += $" {System.Math.Round(Sprite.internalFrames[0].Resolution * 100)}%";
                if (realid.ToString() != id.ToString())
                    info += $" ({realid})";
                Log.Debug(info);
            }
            Sprite.IsNew = false;
            ISpriteProcessor loader = GetSpriteProcessor(id, true);
            loader.Process(id);

            if (loader is ISpriteAnimationProcessor loaderAni)
            {
                for (int i = 0; i < loaderAni.FrameCount && i < Sprite.internalFrames.Length; i++)
                {
                    if (!loaderAni.SetFrame(i))
                    {
                        SpriteFrame[] frames = new SpriteFrame[i];
                        for (int j = 0; j < i; j++)
                            frames[j] = Sprite.Frames[j];
                        Sprite.internalFrames = frames;
                        Sprite.ani.frameCount = i;
                        break;
                    }

                    MemoryUsage += Sprite.internalFrames[i].LoadFromProcessor(loader);
                    Sprite.internalFrames[i].Resolution = Sprite.internalFrames[0].Resolution;
                }
            }
            else
            {
                MemoryUsage += Sprite.internalFrames[0].LoadFromProcessor(loader);
            }

            _engine.DecreaseLoadingCount();
        }
        #endregion

        ISprite IResourceManager.GetImage(ResourceID id, ResourceLoadType loadType) => GetImage(id, loadType);
        void IResourceManager.Reload(ISprite sprite, ResourceLoadType loadType) => Reload(sprite as Sprite, loadType);
    }
}
