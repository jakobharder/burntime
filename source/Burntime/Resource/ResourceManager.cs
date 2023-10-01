﻿using System;
using System.Collections.Generic;
using System.Text;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource
{
    public class ResourceManager : ResourceManagerBase, IResourceManager, IDisposable
    {
        readonly Engine engine;
        
        // global resource register
        Dictionary<String, Sprite> sprites = new Dictionary<String, Sprite>();
        
        // resource processor
        Dictionary<ResourceInfoFont, Font> fonts = new Dictionary<ResourceInfoFont, Font>();
        internal SlimDX.Direct3D9.Texture EmptyTexture;

        DelayLoader delayLoader;

        int memoryPeek;
        int memoryUsage;
        int MemoryUsage
        {
            get { return memoryUsage; }
            set 
            { 
                memoryUsage = value; if (value > memoryPeek) memoryPeek = value;
                Debug.SetInfoMB("sprite memory usage", memoryUsage);
                Debug.SetInfoMB("sprite memory peek", memoryPeek);
            }
        }

        public bool IsLoading
        {
            get { return delayLoader.IsLoading; }
        }

        public ResourceManager(Engine Engine)
        {
            engine = Engine;
            engine.ResourceManager = this;

            memoryUsage = 0;
            Debug.SetInfoMB("sprite memory usage", memoryUsage);
            Debug.SetInfoMB("sprite memory peak", memoryPeek);

            delayLoader = new DelayLoader(this);
        }

        public void Run()
        {
            delayLoader.Run();
        }

        public void Dispose()
        {
            ReleaseAll();   

            Log.Info("texture memory peek: " + (memoryPeek / 1024 / 1024).ToString() + " MB");
            delayLoader.Stop();
        }

        public void Reset()
        {
            delayLoader.Reset();
            fonts.Clear();
            sprites.Clear();
        }

        // common
        internal void ReleaseAll()
        {
            // TODO: critical section
            foreach (Sprite sprite in sprites.Values)
            {
                for (int i = 0; i < sprite.internalFrames.Length; i++)
                {
                    if (sprite.internalFrames[i].texture == null || sprite.internalFrames[i].texture.Disposed)
                        continue;
                    try
                    {
                        SlimDX.Direct3D9.SurfaceDescription desc = sprite.internalFrames[i].texture.GetLevelDescription(0);
                        MemoryUsage -= desc.Width * desc.Height * 4;
                    }
                    catch (Exception)
                    {
                        // TODO make cleaner
                    }
                }

                sprite.Unload();
                Log.Debug("unload \"" + sprite.ID + "\"");
            }

            MemoryUsage = 0;

            foreach (Font font in fonts.Values)
            {
#warning slimdx todo
                //if (font.sprite.internalFrames[0].texture == null)
                //    continue;

                //try
                //{
                //    SlimDX.Direct3D9.SurfaceDescription desc = font.sprite.internalFrames[0].texture.GetLevelDescription(0);
                //    MemoryUsage -= desc.Width * desc.Height * 4;
                //}
                //catch
                //{
                //}

                //font.sprite.Unload();
                //Log.Debug("unload \"" + font.sprite.ID + "\"");
            }
        }

        internal void ReloadAll()
        {
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

            engine.IncreaseLoadingCount();

            IFontProcessor processor = (IFontProcessor)Activator.CreateInstance(fontProcessors[path.Extension].GetType());
            processor.Process((string)path);
            Log.Debug("load \"" + path.FullPath + "\"");

            SlimDX.Direct3D9.Texture tex = engine.Device.CreateTexture(MakePowerOfTwo(processor.Size.x), MakePowerOfTwo(processor.Size.y));

            SlimDX.Direct3D9.SurfaceDescription desc = tex.GetLevelDescription(0);
            MemoryUsage += desc.Width * desc.Height * 4;

            SlimDX.DataRectangle data = tex.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
            System.IO.MemoryStream systemCopy = new System.IO.MemoryStream();
            processor.Render(systemCopy, data.Pitch, ForeColor, BackColor);
            processor.Render(data.Data, data.Pitch, ForeColor, BackColor);
            tex.UnlockRectangle(0);

            //// debug output
            //System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(processor.Size.x, processor.Size.y, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            //System.Drawing.Imaging.BitmapData bmpdata = bmp.LockBits(new System.Drawing.Rectangle(0, 0, processor.Size.x, processor.Size.y), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            //byte[] bytes = new byte[processor.Size.y * bmpdata.Stride];
            //systemCopy.Seek(0, System.IO.SeekOrigin.Begin);

            //for (int y = 0; y < processor.Size.y; y++)
            //{
            //    systemCopy.Read(bytes, y * bmpdata.Stride, processor.Size.x * 4);
            //}

            //System.Runtime.InteropServices.Marshal.Copy(bytes, 0, bmpdata.Scan0, bytes.Length);

            //bmp.UnlockBits(bmpdata);

            //bmp.Save("c:\\font.png");
            //bmp.Dispose();

            SpriteFrame frame = new SpriteFrame(this, tex, processor.Size, systemCopy.ToArray());
            font.sprite = new Sprite(this, "", frame);
            font.sprite.Resolution = processor.Factor;

            font.charInfo = processor.CharInfo;
            font.offset = processor.Offset;
            font.height = processor.Size.y;

            engine.DecreaseLoadingCount();

            fonts.Add(info, font);
            return font;
        }
        #endregion

        #region Sprites
        public Sprite GetImage(ResourceID id, ResourceLoadType loadType = ResourceLoadType.Delayed)
        {
            return GetSprite(id, loadType);
        }

        Sprite GetSprite(ResourceID id, ResourceLoadType LoadType)
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
                    if (sprites.TryGetValue(newid, out Sprite value))
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

                engine.IncreaseLoadingCount();

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

                            frames[i] = new SpriteFrame(this);
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

                        SpriteFrame si = new SpriteFrame(this);
                        s = new Sprite(this, realid, si);
                        s.LoadType = LoadType;

                        sprites.Add(id, s);
                    }
                }

                if (LoadType == ResourceLoadType.Now)
                    Reload(s, ResourceLoadType.Now);

                engine.DecreaseLoadingCount();
                return s;
            }
        }

        internal void Reload(Sprite Sprite, ResourceLoadType LoadType)
        {
            Sprite.internalFrames[0].loading = true;

            if (LoadType == ResourceLoadType.Delayed)
            {
                delayLoader.Enqueue(Sprite);
                return;
            }

            for (int i = 0; i < Sprite.internalFrames.Length; i++)
            {
                if (Sprite.internalFrames[i].HasSystemCopy)
                {
                    SlimDX.Direct3D9.Texture tex = engine.Device.CreateTexture(MakePowerOfTwo(Sprite.internalFrames[i].Size.x), MakePowerOfTwo(Sprite.internalFrames[i].Size.y));
                    SlimDX.Direct3D9.SurfaceDescription desc = tex.GetLevelDescription(0);
                    MemoryUsage += desc.Width * desc.Height * 4;

                    Sprite.internalFrames[i].texture = tex;
                    Sprite.internalFrames[i].RestoreFromSystemCopy();
                    Sprite.internalFrames[i].loading = false;
                    Sprite.internalFrames[i].loaded = true;
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

            engine.IncreaseLoadingCount();

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
                        Sprite.ani.frameCount = i;
                        break;
                    }

                    SlimDX.Direct3D9.Texture tex = engine.Device.CreateTexture(MakePowerOfTwo(loaderAni.FrameSize.x), MakePowerOfTwo(loaderAni.FrameSize.y));

                    SlimDX.Direct3D9.SurfaceDescription desc = tex.GetLevelDescription(0);
                    MemoryUsage += desc.Width * desc.Height * 4;

                    SlimDX.DataRectangle data = tex.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
                    loader.Render(data.Data, data.Pitch);
                    tex.UnlockRectangle(0);

                    Sprite.internalFrames[i].Texture = tex;
                    Sprite.internalFrames[i].Size = loaderAni.FrameSize;
                    Sprite.internalFrames[i].TimeStamp = System.Diagnostics.Stopwatch.GetTimestamp();
                    Sprite.internalFrames[i].Resolution = Sprite.internalFrames[0].Resolution;
                }
            }
            else
            {
                loader.Process(id);

                SlimDX.Direct3D9.Texture tex = engine.Device.CreateTexture(MakePowerOfTwo(loader.Size.x), MakePowerOfTwo(loader.Size.y));

                SlimDX.Direct3D9.SurfaceDescription desc = tex.GetLevelDescription(0);
                MemoryUsage += desc.Width * desc.Height * 4;

                SlimDX.DataRectangle data = tex.LockRectangle(0, SlimDX.Direct3D9.LockFlags.Discard);
                loader.Render(data.Data, data.Pitch);
                tex.UnlockRectangle(0);

                Sprite.internalFrames[0].Texture = tex;
                Sprite.internalFrames[0].Size = loader.Size;
                Sprite.internalFrames[0].TimeStamp = System.Diagnostics.Stopwatch.GetTimestamp();
            }

            Sprite.internalFrames[0].loading = false;
            Sprite.internalFrames[0].loaded = true;

            engine.DecreaseLoadingCount();
        }
        #endregion

        #region DataObject accesss
        Dictionary<string, DataObject> dataObjects = new Dictionary<string, DataObject>();

        public DataObject GetData(ResourceID id, ResourceLoadType loadType = ResourceLoadType.Now)
        {
            DataObject obj;
            if (dataObjects.ContainsKey(id))
            {
                obj = dataObjects[id];
            }
            else if (loadType == ResourceLoadType.Now)
            {
                IDataProcessor processor = GetDataProcessor(id.Format);

                engine.IncreaseLoadingCount();
                obj = processor.Process(id, this);
                Log.Debug("load \"" + id + "\"");
                obj.ResourceManager = this;
                obj.DataName = id;
                obj.PostProcess();
                engine.DecreaseLoadingCount();

                dataObjects.Add(id, obj);
            }
            else
            {
                obj = new NullDataObject(id, this);
            }

            return obj;
        }

        public void RegisterDataObject(ResourceID id, DataObject obj)
        {
            obj.DataName = id;
            obj.ResourceManager = this;

            if (dataObjects.ContainsKey(id))
            {
                Log.Warning("RegisterDataObject: object \"" + id + "\" is already registered!");
                return;
            }

            dataObjects.Add(id, obj);
        }
        #endregion

        ISprite IResourceManager.GetImage(ResourceID id, ResourceLoadType loadType) => GetImage(id, loadType);
        void IResourceManager.Reload(ISprite sprite, ResourceLoadType loadType) => Reload(sprite as Sprite, loadType);
    }
}
