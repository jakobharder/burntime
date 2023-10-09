using System;
using System.Collections.Generic;
using System.Text;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;
using Burntime.SlimDx.Graphics;

namespace Burntime.Platform.Resource
{
    public class ResourceManager : ResourceManagerBase, IResourceManager, IDisposable
    {
        readonly Engine _engine;

        public ResourceManager(Engine engine) : base(engine)
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

            SlimDX.Direct3D9.Texture tex = _engine.Device.CreateTexture(MakePowerOfTwo(processor.Size.x), MakePowerOfTwo(processor.Size.y));

            SlimDX.Direct3D9.SurfaceDescription desc = tex.GetLevelDescription(0);
            lock (sprites)
                MemoryUsage += desc.Width * desc.Height * 4;

            SlimDX.DataRectangle data = tex.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
            System.IO.MemoryStream systemCopy = new System.IO.MemoryStream();
            processor.Color = ForeColor;
            processor.Shadow = BackColor;
            processor.Render(systemCopy, data.Pitch);
            processor.Render(data.Data, data.Pitch);
            tex.UnlockRectangle(0);

            SpriteFrame frame = new SpriteFrame(tex, processor.Size, systemCopy.ToArray());
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
            Vector2f factor = Vector2f.One;
            ResourceID realid = (string)id;

            // check replacements
            if (replacement != null)
            {
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
                            //loaderAni.SetFrame(i);

                            //SlimDX.Direct3D9.Texture tex = new SlimDX.Direct3D9.Texture(engine.Device, loaderAni.FrameSize.x, loaderAni.FrameSize.y, 1,
                            //    SlimDX.Direct3D9.Usage.Dynamic, SlimDX.Direct3D9.Format.A8R8G8B8,
                            //    SlimDX.Direct3D9.Pool.Default);

                            //SlimDX.DataRectangle data = tex.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
                            //loader.Render(data.Data, data.Pitch);
                            //tex.UnlockRectangle(0);

                            frames[i] = new SpriteFrame();
                        }

                        s = new Sprite(this, realid, frames, new SpriteAnimation(loaderAni.FrameCount));
                        s.LoadType = LoadType;

                        sprites.Add(id, s);
                    }
                    else
                    {
                        //loader.Process(id);

                        //SlimDX.Direct3D9.Texture tex = new SlimDX.Direct3D9.Texture(engine.Device, loader.Size.x, loader.Size.y, 1,
                        //    SlimDX.Direct3D9.Usage.Dynamic, SlimDX.Direct3D9.Format.A8R8G8B8,
                        //    SlimDX.Direct3D9.Pool.Default);

                        //SlimDX.DataRectangle data = tex.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
                        //loader.Render(data.Data, data.Pitch);
                        //tex.UnlockRectangle(0);

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
                if (Sprite.internalFrames[i].HasSystemCopy)
                {
                    SlimDX.Direct3D9.Texture tex = _engine.Device.CreateTexture(MakePowerOfTwo(Sprite.internalFrames[i].Size.x), MakePowerOfTwo(Sprite.internalFrames[i].Size.y));
                    SlimDX.Direct3D9.SurfaceDescription desc = tex.GetLevelDescription(0);
                    lock (sprites)
                        MemoryUsage += desc.Width * desc.Height * 4;

                    Sprite.internalFrames[i].Texture = tex;
                    Sprite.internalFrames[i].RestoreFromSystemCopy();
                    Sprite.internalFrames[i].SetLoaded();
                }
            }
 
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
                if (Sprite.internalFrames[0].Resolution != Vector2f.One)
                    info += $" " + Log.FormatPercentage(Sprite.internalFrames[0].Resolution);
                if (realid.ToString() != id.ToString())
                    info += $" ({realid})";
                Log.Debug(info);
            }
            Sprite.IsNew = false;
            ISpriteProcessor loader = GetSpriteProcessor(id, true);
            if (loader is ISpriteAnimationProcessor loaderAni)
            {
                loader.Process(id);

                for (int i = 0; i < loaderAni.FrameCount && i < Sprite.internalFrames.Length; i++)
                {
                    if (!loaderAni.SetFrame(i))
                    {
                        SpriteFrame[] frames = new SpriteFrame[i];
                        for (int j = 0; j < i; j++)
                            frames[j] = Sprite.Frames[j];
                        Sprite.internalFrames = frames;
                        Sprite.ani.FrameCount = i;
                        break;
                    }

                    SlimDX.Direct3D9.Texture tex = _engine.Device.CreateTexture(MakePowerOfTwo(loaderAni.FrameSize.x), MakePowerOfTwo(loaderAni.FrameSize.y));

                    SlimDX.Direct3D9.SurfaceDescription desc = tex.GetLevelDescription(0);
                    lock (sprites)
                        MemoryUsage += desc.Width * desc.Height * 4;

                    SlimDX.DataRectangle data = tex.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
                    loader.Render(data.Data, data.Pitch);
                    tex.UnlockRectangle(0);

                    Sprite.internalFrames[i].Texture = tex;
                    Sprite.internalFrames[i].Size = loaderAni.FrameSize;
                    Sprite.internalFrames[i].SetLoaded();
                    Sprite.internalFrames[i].Resolution = Sprite.internalFrames[0].Resolution;
                }
            }
            else
            {
                loader.Process(id);

                SlimDX.Direct3D9.Texture tex = _engine.Device.CreateTexture(MakePowerOfTwo(loader.Size.x), MakePowerOfTwo(loader.Size.y));

                SlimDX.Direct3D9.SurfaceDescription desc = tex.GetLevelDescription(0);
                lock (sprites)
                    MemoryUsage += desc.Width * desc.Height * 4;

                SlimDX.DataRectangle data = tex.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
                loader.Render(data.Data, data.Pitch);
                tex.UnlockRectangle(0);

                Sprite.internalFrames[0].Texture = tex;
                Sprite.internalFrames[0].Size = loader.Size;
                Sprite.internalFrames[0].SetLoaded();
            }

            _engine.DecreaseLoadingCount();
        }
        #endregion

        ISprite IResourceManager.GetImage(ResourceID id, ResourceLoadType loadType) => GetImage(id, loadType);
        void IResourceManager.Reload(ISprite sprite, ResourceLoadType loadType) => Reload(sprite as SlimDx.Graphics.Sprite, loadType);
    }
}
