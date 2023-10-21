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
    Vector2 _native = Vector2.Zero;

    public Vector2 MinResolution
    {
        get => _minResolution;
        set
        {
            _minResolution = value;
            SelectBestGameResolution();
        }
    }
    Vector2 _minResolution = Vector2.Zero;

    public Vector2 MaxResolution
    {
        get => _maxResolution;
        set
        {
            _maxResolution = value;
            SelectBestGameResolution();
        }
    }
    Vector2 _maxResolution = Vector2.Zero;

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

    void SelectBestGameResolution()
    {
        if (MinResolution.x == 0 || MaxResolution.x == 0 || Native.x == 0) return;

        const int DOUBLED_RESOLUTION = 2;

        Vector2f min = (Vector2f)MinResolution * DOUBLED_RESOLUTION * _ratioCorrection;
        Vector2f max = (Vector2f)MaxResolution * DOUBLED_RESOLUTION * _ratioCorrection;

        Vector2 maxFactor = ((Vector2f)_native / max).Floor();
        Vector2 minFactor = ((Vector2f)_native / min).Floor();
        int verticalFactor = Math.Max(1, maxFactor.y, minFactor.y);
        int horizontalFactor = Math.Max(1, maxFactor.x, minFactor.x);

        int factor = Math.Min(verticalFactor, horizontalFactor);

        BackBuffer = _native / factor;

        Scale = DOUBLED_RESOLUTION * _ratioCorrection;
        Game = (Vector2f)BackBuffer / Scale;

#warning use render to texture instead and strech that by verticalfactor?
        Scale *= factor;
    }
}
