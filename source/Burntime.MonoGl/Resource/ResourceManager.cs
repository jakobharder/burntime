using System;
using System.Collections.Generic;
using System.Text;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;

namespace Burntime.Platform.Resource
{
    struct Replacement
    {
        public String Argument;
        public String Value;
    }

    struct ResourceInfoFont
    {
        public String Name;
        public PixelColor Fore;
        public PixelColor Back;
    }

    public class ResourceManager : IResourceManager, IDisposable
    {
        Engine engine;
        
        // global resource register
        Dictionary<String, Sprite> sprites = new Dictionary<String, Sprite>();
        Dictionary<string, DataObject> dataObjects = new Dictionary<string, DataObject>();
        
        // resource processor
        Dictionary<String, ISpriteProcessor> spriteProcessors = new Dictionary<string, ISpriteProcessor>();
        Dictionary<ResourceInfoFont, Font> fonts = new Dictionary<ResourceInfoFont, Font>();
        internal SlimDX.Direct3D9.Texture EmptyTexture;
        
        Dictionary<String, IFontProcessor> fontProcessors = new Dictionary<string, IFontProcessor>();
        Dictionary<String, Type> dataProcessors = new Dictionary<string, Type>();

        DelayLoader delayLoader;
        Encoding encoding;
        ConfigFile replacement;

        public Encoding Encoding
        {
            get { return encoding; }
            set { encoding = value; }
        }

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
            AddSpriteProcessor("png", new SpriteProcessorPng());
            AddDataProcessor("png", typeof(SpriteProcessorPng));
            AddSpriteProcessor("pngani", new AniProcessorPng());
            AddDataProcessor("pngani", typeof(AniProcessorPng));
            AddFontProcessor("txt", new FontProcessorTxt());

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

        // processor
        public void AddSpriteProcessor(String Extension, ISpriteProcessor Processor)
        {
            spriteProcessors.Add(Extension, Processor);
        }

        public void AddFontProcessor(String Extension, IFontProcessor Processor)
        {
            fontProcessors.Add(Extension, Processor);
        }

        public void AddDataProcessor(String format, Type dataProcessor)
        {
            dataProcessors.Add(format, dataProcessor);
        }

        public IDataProcessor GetDataProcessor(String Format)
        {
            return (IDataProcessor)Activator.CreateInstance(dataProcessors[Format]);
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

        // font
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

        // sprites
        public Sprite GetImage(ResourceID id, ResourceLoadType loadType = ResourceLoadType.Delayed)
        {
            return GetSprite(id, loadType);
        }

        //Sprite GetSprite(string id, int index)
        //{
        //    id = id.ToLower();
        //    return GetSprite(id + "?" + index.ToString());
        //}

        ResourceID GetReplacementID(ResourceID id)
        {
            if (replacement != null)
            {
                string idstring = null;

                if (replacement["replacement"].ContainsKey(id.Format + "@" + id.File))
                {
                    idstring = replacement["replacement"].Get(id.Format + "@" + id.File);
                }
                else if (replacement["replacement"].ContainsKey(id.File))
                {
                    idstring = replacement["replacement"].Get(id.File);
                }

                if (idstring != null)
                {

                    // construct new resource id
                    if (id.IndexProvided)
                    {
                        idstring += "?" + id.Index.ToString();
                        if (id.EndIndex != -1)
                            idstring += "-" + id.EndIndex.ToString();
                        if (!string.IsNullOrEmpty(id.Custom))
                            idstring += "?" + id.Custom;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(id.Custom))
                            idstring += "??" + id.Custom;
                    }

                    return idstring;
                }
            }
            
            return null;
        }

        bool CheckReplacementID(ResourceID id)
        {
            string format = id.Format;
            if (spriteProcessors.ContainsKey(format))
            {
                lock (this)
                {
                    ISpriteProcessor loader = spriteProcessors[format];
                    //loader.IsAvailable(newid);

                    // TODO
                    // for the moment just check the newid.file file existance
                    return FileSystem.ExistsFile(string.Format(id.File, id.Index));
                }
            }

            return false;
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

        #region DataObject accesss
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

        private ISpriteProcessor GetSpriteProcessor(ResourceID id, bool ownInstance)
        {

#warning this should be handled differently

            string format = id.Format;
            if (id.Format == "raw" && id.HasMultipleFrames)
            {
                format = "ani";
            }

            if (ownInstance)
                return (ISpriteProcessor)Activator.CreateInstance(spriteProcessors[format].GetType());

            return spriteProcessors[format];
        }

        // from textDB

        Dictionary<String, TextResourceFile> txtDB = new Dictionary<string, TextResourceFile>();
        List<Replacement> listArguments = new List<Replacement>();

        public void AddDB(string filename)
        {
            File file = FileSystem.GetFile(filename + (filename.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) ? "" : ".txt"));
            file.Encoding = encoding;
            TextResourceFile db = new TextResourceFile(file);
            txtDB.Add(filename, db);
        }

        public void AddArgument(String Argument, int Value)
        {
            AddArgument(Argument, Value.ToString());
        }

        public void AddArgument(String Argument, String Value)
        {
            Replacement repl = new Replacement();
            repl.Argument = Argument;
            repl.Value = Value;
            listArguments.Add(repl);
        }

        public void ClearArguments()
        {
            listArguments.Clear();
        }

        public String ShiftID(String id, int shift)
        {
            String fileName;
            int pos = shift;
            int atmark = id.LastIndexOf('?');
            if (atmark > 0)
            {
                fileName = id.Substring(0, atmark);
                pos += int.Parse(id.Substring(atmark + 1));
            }
            else
                fileName = id;

            return fileName + "?" + pos;
        }

        public String[] GetStrings(String id)
        {
            int atmark = id.LastIndexOf('?');

            string file = id.Substring(0, atmark);

            int index = 0;
            if (id.Substring(atmark + 1).ToLower().StartsWith("s"))
            {
                if (!txtDB.ContainsKey(file))
                    AddDB(file);

                int section = int.Parse(id.Substring(atmark + 2));

                int sections = 0;
                for (int i = 0; i < txtDB[file].Data.Count && sections < section; i++)
                {
                    int f = txtDB[file].Data[i].IndexOf("}#");
                    if (f != -1)
                    {
                        sections++;
                        index = i + 1;
                    }
                }
            }
            else
                index = int.Parse(id.Substring(atmark + 1));

            return GetStrings(file, index);
        }

        public String[] GetStrings(String file, int startIndex)
        {
            if (!txtDB.ContainsKey(file))
                AddDB(file);

            int last = startIndex + 1;

            for (int i = startIndex; i < txtDB[file].Data.Count; i++)
            {
                int f = txtDB[file].Data[i].IndexOf("}#");
                if (f != -1)
                {
                    last = i;
                    break;
                }
            }

            int count = last - startIndex;
            String[] strs = new String[count];
            for (int i = 0; i < count; i++)
            {
                strs[i] = txtDB[file].Data[i + startIndex].Replace("}", "");
            }

            return strs;
        }

        public string GetString(string id)
        {
            if (id.StartsWith("@"))
                id = id.Substring(1);

            int atmark = id.LastIndexOf('?');
            return GetString(id.Substring(0, atmark), int.Parse(id.Substring(atmark + 1)));
        }

        public string GetString(string file, int index)
        {
            if (!txtDB.ContainsKey(file))
                AddDB(file);
            string res = txtDB[file].Data[index];

            if (res.EndsWith("}"))
                return res.Substring(0, res.Length - 1);
            return res;
        }

        public void SetResourceReplacement(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                replacement = null;
            }
            else
            {
                replacement = new ConfigFile();
                replacement.Open(FileSystem.GetFile(file));
            }
        }

        int MakePowerOfTwo(int nValue)
        {
            nValue--;
            int i;
            for (i = 0; nValue != 0; i++)
                nValue >>= 1;
            return 1 << i;
        }

        ISprite IResourceManager.GetImage(ResourceID id, ResourceLoadType loadType) => GetImage(id, loadType);
        void IResourceManager.Reload(ISprite sprite, ResourceLoadType loadType) => Reload(sprite as Sprite, loadType);
    }
}
