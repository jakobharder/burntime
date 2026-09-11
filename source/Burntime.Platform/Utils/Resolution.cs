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
    public float OutputScale { get; private set; } = 1;
    public Vector2 Game { get; private set; } = new(960, 640);
    public Vector2 BackBuffer { get; private set; } = new(960, 540);

    public float? OutputScaleOverride
    {
        get => _outputScaleOverride;
        set
        {
            _outputScaleOverride = value;
            SelectBestGameResolution();
        }
    }
    float? _outputScaleOverride;

    void SelectBestGameResolution()
    {
        if (MinResolution.x == 0 || MaxResolution.x == 0 || Native.x == 0) return;

        const int DOUBLED_RESOLUTION = 2;

        Vector2f min = (Vector2f)MinResolution * DOUBLED_RESOLUTION * _ratioCorrection;
        // Half-step output scales keep low-resolution displays near the same
        // logical viewport as their higher-resolution multiples. For example,
        // 720p at 1.5x matches 1440p at 3x instead of expanding to 682x320.
        float verticalFactor = MathF.Floor(_native.y / min.y * 2) / 2;
        float horizontalFactor = MathF.Floor(_native.x / min.x * 2) / 2;
        float automaticFactor = System.MathF.Max(1,
            System.MathF.Min(verticalFactor, horizontalFactor));
        OutputScale = _outputScaleOverride ?? automaticFactor;

        BackBuffer = (Vector2f)_native / OutputScale;

        Scale = DOUBLED_RESOLUTION * _ratioCorrection;
        Game = (Vector2f)BackBuffer / Scale;
    }
}
