using Burntime.Platform.IO;
using Burntime.Platform.Graphics;
using System.Diagnostics;
using System.Text;
using System.Runtime.InteropServices.ComTypes;

namespace Burntime.Platform.Resource;

public struct Replacement
{
    public string Argument;
    public string Value;
}

public struct ResourceInfoFont
{
    public string Name;
    public PixelColor Fore;
    public PixelColor Back;
}

public abstract class ResourceManagerBase : IResourceManager
{
    protected readonly Dictionary<string, ISprite> sprites = [];
    protected readonly Dictionary<ResourceInfoFont, FontResource> fonts = [];
    protected readonly DelayLoader delayLoader;
    public bool IsLoading => delayLoader.IsLoading;

    protected ILoadingCounter _loadingCounter;

    public ResourceManagerBase(ILoadingCounter loadingCounter)
    {
        _loadingCounter = loadingCounter;

        AddSpriteProcessor("png", new SpriteProcessorPng());
        AddDataProcessor("png", typeof(SpriteProcessorPng));
        AddSpriteProcessor("pngani", new AniProcessorPng());
        AddDataProcessor("pngani", typeof(AniProcessorPng));
        AddSpriteProcessor("pngsheet", new PngSpriteSheetProcessor());
        AddDataProcessor("pngsheet", typeof(PngSpriteSheetProcessor));
        AddFontProcessor("txt", new FontProcessorTxt());

        delayLoader = new DelayLoader(this);

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
            foreach (FontResource font in fonts.Values)
            {
                if (!font.IsLoaded)
                    continue;

                string id = font.Sprite.ID;
                MemoryUsage -= font.Unload();
                Log.Debug("unload \"" + id + "\"");
            }
        }
    }

    public void ReloadAll()
    {
    }

    public virtual Font? LoadFont(Font font) { return font; }

    public abstract ISprite GetImage(ResourceID id, ResourceLoadType loadType = ResourceLoadType.Delayed);
    public abstract Font? GetFont(string file, PixelColor color);
    public abstract Font? GetFont(string file, PixelColor color, PixelColor backColor);
    public abstract void Reload(ISprite sprite, ResourceLoadType loadType = ResourceLoadType.Delayed);

    #region Text
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    // from textDB

    readonly Dictionary<string, TextResourceFile> txtDB = [];
    readonly List<Replacement> listArguments = [];

    public void AddDB(string filename)
    {
        IO.File file = FileSystem.GetFile(filename + (filename.EndsWith(".txt", StringComparison.InvariantCultureIgnoreCase) ? "" : ".txt"));
        file.Encoding = Encoding;
        TextResourceFile db = new TextResourceFile(file);
        txtDB.Add(filename, db);
    }

    public void AddArgument(string Argument, int Value)
    {
        AddArgument(Argument, Value.ToString());
    }

    public void AddArgument(string Argument, string Value)
    {
        Replacement repl = new()
        {
            Argument = Argument,
            Value = Value
        };
        listArguments.Add(repl);
    }

    public void ClearArguments()
    {
        listArguments.Clear();
    }

    public string ShiftID(string id, int shift)
    {
        string fileName;
        int pos = shift;
        int atmark = id.LastIndexOf('?');
        if (atmark > 0)
        {
            fileName = id[..atmark];
            pos += int.Parse(id[(atmark + 1)..]);
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
        string[] strs = new string[count];
        for (int i = 0; i < count; i++)
        {
            strs[i] = txtDB[file].Data[i + startIndex].Replace("}", "");
        }

        return strs;
    }

    public string GetString(string id)
    {
        if (id.StartsWith("@"))
            id = id[1..];

        int atmark = id.LastIndexOf('?');
        return GetString(id[..atmark], int.Parse(id[(atmark + 1)..]));
    }

    public string GetString(string file, int index)
    {
        if (!txtDB.ContainsKey(file))
            AddDB(file);
        string res = txtDB[file].Data[index];

        if (res.EndsWith("}"))
            return res[..^1];
        return res;
    }

    public void ClearText()
    {
        txtDB.Clear();
    }
    #endregion

    #region DataProcessor
    protected Dictionary<string, IFontProcessor> fontProcessors = [];
    protected Dictionary<string, Type> dataProcessors = [];

    public void AddSpriteProcessor(string Extension, ISpriteProcessor Processor)
    {
        spriteProcessors.Add(Extension, Processor);
    }

    public void AddFontProcessor(string Extension, IFontProcessor Processor)
    {
        fontProcessors.Add(Extension, Processor);
    }

    public void AddDataProcessor(string format, Type dataProcessor)
    {
        dataProcessors.Add(format, dataProcessor);
    }

    public IDataProcessor GetDataProcessor(string Format)
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

                #warning TODO for the moment just check the newid.file file existance
                return FileSystem.ExistsFile(string.Format(id.File, id.Index));
            }
        }

        return false;
    }
    #endregion

    #region DataObject access
    readonly Dictionary<string, DataObject> dataObjects = [];

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
            obj = processor.Process(id, this);
            Log.Debug("load \"" + id + "\"");
            obj.ResourceManager = this;
            obj.DataName = id;
            obj.PostProcess();
            _loadingCounter.DecreaseLoadingCount();

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

    protected int _memoryPeek;
    int _memoryUsage;

    /// <summary>
    /// Estimated memory used by loaded textures, in bytes.
    /// </summary>
    public int TextureMemoryUsage => Volatile.Read(ref _memoryUsage);

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

    protected Dictionary<string, ISpriteProcessor> spriteProcessors = [];
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
