using Burntime.Platform.IO;
using System.Collections.Generic;
using System;
using System.Text;
using System.Runtime.InteropServices.ComTypes;
using Burntime.Platform.Graphics;
using System.Diagnostics;

namespace Burntime.Platform.Resource;

public struct Replacement
{
    public String Argument;
    public String Value;
}

public struct ResourceInfoFont
{
    public String Name;
    public PixelColor Fore;
    public PixelColor Back;
}

public class ResourceManagerBase
{
    protected readonly Dictionary<string, ISprite> sprites = new();
    protected readonly Dictionary<ResourceInfoFont, Font> fonts = new();
    protected readonly DelayLoader delayLoader;
    public bool IsLoading => delayLoader.IsLoading;

    protected ILoadingCounter _loadingCounter;

    public ResourceManagerBase(ILoadingCounter loadingCounter)
    {
        _loadingCounter= loadingCounter;

        AddSpriteProcessor("png", new SpriteProcessorPng());
        AddDataProcessor("png", typeof(SpriteProcessorPng));
        AddSpriteProcessor("pngani", new AniProcessorPng());
        AddDataProcessor("pngani", typeof(AniProcessorPng));
        AddSpriteProcessor("pngsheet", new PngSpriteSheetProcessor());
        AddDataProcessor("pngsheet", typeof(PngSpriteSheetProcessor));
        AddFontProcessor("txt", new FontProcessorTxt());

        delayLoader = new DelayLoader(this as IResourceManager);

#warning TODO SlimDX/Mono debug info
        //Debug.SetInfoMB("sprite memory usage", MemoryUsage);
        //Debug.SetInfoMB("sprite memory peak", _memoryPeek);
    }

    public void Run()
    {
        delayLoader.Run();
    }

    public void Dispose()
    {
        ReleaseAll();

        Log.Info("texture memory peek: " + (_memoryPeek / 1024 / 1024).ToString() + " MB");
        delayLoader.Stop();
    }

    public void Reset()
    {
        delayLoader.Reset();
        fonts.Clear();
        sprites.Clear();
    }

    public void ReleaseAll()
    {
        lock (sprites)
        {
            foreach (ISprite sprite in sprites.Values)
            {
                MemoryUsage -= sprite.Unload();
                Log.Debug("unload \"" + sprite.ID + "\"");
            }
        }

        lock (fonts)
        {
            foreach (Font font in fonts.Values)
            {
                MemoryUsage -= font.sprite.Unload();
                font.IsLoaded = false;
                Log.Debug("unload \"" + font.sprite.ID + "\"");
            }
            fonts.Clear();
        }
    }

    public void ReloadAll()
    {
    }

    public virtual Font? LoadFont(Font font) { return font; }

    #region Text
    public Encoding? Encoding { get; set; }

    // from textDB

    Dictionary<String, TextResourceFile> txtDB = new Dictionary<string, TextResourceFile>();
    List<Replacement> listArguments = new List<Replacement>();

    public void AddDB(string filename)
    {
        IO.File file = FileSystem.GetFile(filename + (filename.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) ? "" : ".txt"));
        file.Encoding = Encoding;
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

    private int GetSectionStart(string fileName, int sectionNumber)
    {
        // lazy load
        if (!txtDB.ContainsKey(fileName))
            AddDB(fileName);

        int index = 0;
        int sections = 0;
        for (int i = 0; i < txtDB[fileName].Data.Count && sections < sectionNumber; i++)
        {
            if ("}#" == txtDB[fileName].Data[i])
            {
                sections++;
                index = i + 1;
            }
        }

        return index;
    }

    private int GetSectionEnd(string fileName, int startIndex)
    {
        int endIndex = txtDB[fileName].Data.IndexOf("}#", startIndex);
        return endIndex == -1 ? startIndex : endIndex;
    }

    /// <summary>
    /// Get all strings until next }# marker.
    /// Use ?s<number> to use section instead of line number.
    /// </summary>
    public string[] GetStrings(string id)
    {
        int lineMarker = id.LastIndexOf('?');
        string filePart = id[..lineMarker];
        string indexPart = id[(lineMarker + 1)..];

        bool indexIsSection = indexPart.StartsWith("s");
        if (!indexIsSection)
            return GetStrings(filePart, int.Parse(indexPart));

        int section = int.Parse(id[(lineMarker + 2)..]);
        return GetStrings(filePart, GetSectionStart(filePart, section));
    }

    /// <summary>
    /// Get all strings until next }# marker starting from startIndex.
    /// </summary>
    public string[] GetStrings(string file, int startIndex)
    {
        if (!txtDB.ContainsKey(file))
            AddDB(file);

        int sectionEnd = GetSectionEnd(file, startIndex);

        int count = sectionEnd - startIndex;
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

    public void ClearText()
    {
        txtDB.Clear();
    }
    #endregion

    #region DataProcessor
    protected Dictionary<String, IFontProcessor> fontProcessors = new Dictionary<string, IFontProcessor>();
    protected Dictionary<String, Type> dataProcessors = new Dictionary<string, Type>();

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
    #endregion

    #region Replacement
    protected ConfigFile? replacement;

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

    public class ScaledResourceId
    {
        public ResourceID Id;
        public Vector2f Factor;

        public ScaledResourceId(ResourceID id, Vector2f factor)
        {
            Id = id;
            Factor = factor;
        }
    }

    public ScaledResourceId? GetReplacement(ResourceID id)
    {
        if (replacement is null) return null;

        foreach (var section in replacement.GetAllSections())
        {
            var replacedId = GetReplacementID(id, section);
            if (replacedId is not null)
            {
                var scale = section.GetVector2f("sprite_scale", Vector2f.One);
                var factor = (scale != Vector2f.Zero) ? Vector2f.One / scale : 1;
                return new ScaledResourceId(replacedId, factor);
            }
        }

        return null;
    }

    private static ResourceID? GetReplacementID(ResourceID id, ConfigSection section)
    {
        string? idstring = null;

        if (section.ContainsKey(id.ToString()))
        {
            return section.Get(id.ToString());
        }
        else if (section.ContainsKey(id.Format + "@" + id.File))
        {
            idstring = section.Get(id.Format + "@" + id.File);
        }
        else if (section.ContainsKey(id.File))
        {
            idstring = section.Get(id.File);
        }

        if (idstring is null)
            return null;

        // construct new resource id
        if (id.IndexProvided)
        {
            if (idstring.Contains('?') && idstring.Split("?")[1].Contains("{0}"))
            {
                if (id.EndIndex != -1)
                    idstring = idstring.Replace("{0}", $"{id.Index}-{id.EndIndex}");
                else
                    idstring = idstring.Replace("{0}", id.Index.ToString());
            }
            else
            {
                idstring += "?" + id.Index.ToString();
                if (id.EndIndex != -1)
                    idstring += "-" + id.EndIndex.ToString();
                if (!string.IsNullOrEmpty(id.Custom))
                    idstring += "?" + id.Custom;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(id.Custom))
                idstring += "??" + id.Custom;
        }

        return idstring;
    }

    protected bool CheckReplacementID(ResourceID id)
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
    #endregion

    #region DataObject access
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

            _loadingCounter.IncreaseLoadingCount();
            obj = processor.Process(id, this as IResourceManager);
            Log.Debug("load \"" + id + "\"");
            obj.ResourceManager = this as IResourceManager;
            obj.DataName = id;
            obj.PostProcess();
            _loadingCounter.DecreaseLoadingCount();

            dataObjects.Add(id, obj);
        }
        else
        {
            obj = new NullDataObject(id, this as IResourceManager);
        }

        return obj;
    }

    public void RegisterDataObject(ResourceID id, DataObject obj)
    {
        obj.DataName = id;
        obj.ResourceManager = this as IResourceManager;

        if (dataObjects.ContainsKey(id))
        {
            Log.Warning("RegisterDataObject: object \"" + id + "\" is already registered!");
            return;
        }

        dataObjects.Add(id, obj);
    }
    #endregion

    protected int _memoryPeek;
    int _memoryUsage;
    protected int MemoryUsage
    {
        get { return _memoryUsage; }
        set
        {
            _memoryUsage = value; if (value > _memoryPeek) _memoryPeek = value;
#warning TODO SlimDX/Mono debug info
            //Debug.SetInfoMB("sprite memory usage", _memoryUsage);
            //Debug.SetInfoMB("sprite memory peek", _memoryPeek);
        }
    }

    protected static int MakePowerOfTwo(int nValue)
    {
        nValue--;
        int i;
        for (i = 0; nValue != 0; i++)
            nValue >>= 1;
        return 1 << i;
    }

    protected Dictionary<String, ISpriteProcessor> spriteProcessors = new Dictionary<string, ISpriteProcessor>();
    protected ISpriteProcessor GetSpriteProcessor(ResourceID id, bool ownInstance)
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
}
