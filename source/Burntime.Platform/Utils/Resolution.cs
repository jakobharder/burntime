using static System.Formats.Asn1.AsnWriter;

namespace Burntime.Platform.Utils;

public class Resolution
{
    public Vector2 Native
    {
        get => _native;
        set
        {
            _native = value;
            SelectBestGameResolution();
        }
    }
    Vector2 _native = new(960, 540);

    public int MaxVerticalResolution
    {
        get => _maxVerticalResolution;
        set
        {
            _maxVerticalResolution = value;
            SelectBestGameResolution();
        }
    }
    int _maxVerticalResolution = 0;

    Vector2f _ratioCorrection = Vector2f.One;
    public Vector2f RatioCorrection
    {
        get => _ratioCorrection;
        set
        {
            _ratioCorrection = value;
            SelectBestGameResolution();
        }
    }

    public Vector2f Scale { get; private set; } = new(1, 1);
    public Vector2 Game { get; private set; } = new(960, 640);
    public Vector2 BackBuffer { get; private set; } = new(960, 540);

    static bool IsCleanZoom(float zoom)
    {
        return zoom == 4 || zoom == 3 || zoom == 2 || zoom == 1 || zoom == 0.5f;
    }

    void SelectBestGameResolution()
    {
        if (_maxVerticalResolution == 0) return;

        const float DOUBLED_RESOLUTION = 2;
        float MaxHeight = _maxVerticalResolution * DOUBLED_RESOLUTION;
        int verticalFactor = 1;

        while (_native.y > MaxHeight * verticalFactor)
            verticalFactor++;

        BackBuffer = _native / verticalFactor;

        Scale = DOUBLED_RESOLUTION * _ratioCorrection;
        Game = (Vector2)((Vector2f)BackBuffer / Scale);

#warning use render to texture instead and strech that by verticalfactor?
        Scale *= verticalFactor;
    }
}
