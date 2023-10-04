using System;
using System.IO;
using Burntime.Platform.Resource;

namespace Burntime.Platform.Graphics;

public class PngSpriteSheetProcessor : ISpriteProcessor, ISpriteAnimationProcessor, IDataProcessor
{
    Vector2 _size;
    int _frameOffset;
    int _frameCount;
    string _format = "";
    SpriteProcessorPng? _png;

    int _columns;
    int _rows;
    int _frame;

    public Vector2 Size
    {
        get { return _size; }
    }

    public int FrameCount
    {
        get { return _frameCount; }
    }

    public Vector2 FrameSize
    {
        get { return _size; }
    }

    public bool SetFrame(int frame)
    {
        if (_png is null)
        {
            _png = new SpriteProcessorPng();
            _png.Process(_format);

            _columns = (int)System.Math.Floor(_png.Size.x / (float)_size.x);
            _rows = (int)System.Math.Ceiling((_frameOffset + _frameCount) / (float)_columns);
        }
        _frame = frame + _frameOffset;
        return true;
    }

    public void Process(ResourceID id)
    {
        var parseError = () => Log.Warning($"ani: resource id '{id}' is invalid");

        if (id.EndIndex == -1)
            _frameCount = 1;
        else
            _frameCount = id.EndIndex - id.Index + 1;

        _frameOffset = id.Index;
        _format = id.File;
        _size = new Vector2(1, 1);
        _png = null;

        if (string.IsNullOrEmpty(id.Custom))
        {
            parseError();
            return;
        }
        
        string[] size = id.Custom.Split("x");
        if (size.Length != 2)
        {
            parseError();
            return;
        }
        if (!int.TryParse(size[0], out _size.x))
        {
            parseError();
            return;
        }
        if (!int.TryParse(size[1], out _size.y))
        {
            parseError();
            return;
        }
    }

    public DataObject Process(ResourceID ID, IResourceManager ResourceManager)
    {
        return ResourceManager.GetImage(ID);
    }

    public void Render(IntPtr ptr)
    {
        throw new NotSupportedException();
    }

    public void Render(System.IO.Stream s, int stride)
    {
        if (_png is null) return;

        int _row = _frame / _columns;
        if (_row >= _rows)
        {
            Log.Warning($"ani: row {_row} is out of bounds for {_format}");
            return;
        }

        for (int y = _row * _size.y; y < (_row + 1) * _size.y; y++)
        {
            s.Write(_png.Buffer, y * _png.Size.x * 4 + (_frame % _columns) * _size.x * 4, _size.x * 4);
            s.Seek(stride - _size.x * 4, SeekOrigin.Current);
        }
    }

    string[] IDataProcessor.Names
    {
        get { return new string[] { "pngsheet" }; }
    }
}
