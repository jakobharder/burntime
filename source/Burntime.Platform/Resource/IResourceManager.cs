using Burntime.Platform.Graphics;
using System;
using System.Text;

namespace Burntime.Platform.Resource;

public enum ResourceLoadType
{
    Now,
    Delayed,
    LinkOnly
}

public interface IResourceManager
{
    Encoding Encoding { get; set; }

    DataObject GetData(ResourceID id, ResourceLoadType loadType = ResourceLoadType.Now);
    void RegisterDataObject(ResourceID id, DataObject obj);

    ISprite GetImage(ResourceID id, ResourceLoadType loadType = ResourceLoadType.Delayed);
    Font? GetFont(string file, PixelColor color);
    Font? GetFont(string file, PixelColor color, PixelColor backColor);
    string GetString(string id);
    string GetString(string file, int index);
    public string[] GetStrings(string id);
    public string[] GetStrings(string file, int startIndex);

    void Reload(ISprite sprite, ResourceLoadType loadType = ResourceLoadType.Delayed);

    void AddSpriteProcessor(string Extension, ISpriteProcessor Processor);
    void AddFontProcessor(string Extension, IFontProcessor Processor);
    void AddDataProcessor(string format, Type dataProcessor);
    IDataProcessor GetDataProcessor(string format);

    void SetResourceReplacement(string file);

    //LoadingCounter LoadingCounter { get; }

    void ClearText();
}
