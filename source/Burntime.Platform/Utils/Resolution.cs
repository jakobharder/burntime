namespace Burntime.Platform.Utils;

public readonly record struct SafeAreaInsets(int Left, int Top, int Right, int Bottom);

public class Resolution
{
    Vector2 minimumSafeGameSize;
    public Vector2 MinimumSafeGameSize
    {
        get => minimumSafeGameSize;
        set { minimumSafeGameSize = value; SelectBestGameResolution(); }
    }

    SafeAreaInsets nativeSafeAreaInsets;
    // Insets are relative to the actual game view, in backbuffer pixels.
    public SafeAreaInsets NativeSafeAreaInsets
    {
        get => nativeSafeAreaInsets;
        set { nativeSafeAreaInsets = value; SelectBestGameResolution(); }
    }

    public Rect SafeArea
    {
        get
        {
            int left = Inset(NativeSafeAreaInsets.Left, Native.x, Game.x);
            int top = Inset(NativeSafeAreaInsets.Top, Native.y, Game.y);
            int right = Inset(NativeSafeAreaInsets.Right, Native.x, Game.x);
            int bottom = Inset(NativeSafeAreaInsets.Bottom, Native.y, Game.y);
            // Invalid or transient platform geometry must not collapse the UI.
            if (left + right >= Game.x || top + bottom >= Game.y)
                return new Rect(Vector2.Zero, Game);
            return new Rect(left, top, Game.x - left - right, Game.y - top - bottom);
        }
    }

    static int Inset(int pixels, int native, int logical) => native <= 0 ? 0 :
        (int)System.Math.Ceiling(System.Math.Clamp(pixels, 0, native) * (double)logical / native);

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

    // Phone layouts use a stable logical height independently of pixel density.
    public int? FixedGameHeight
    {
        get => _fixedGameHeight;
        set
        {
            _fixedGameHeight = value is > 0 ? value : null;
            SelectBestGameResolution();
        }
    }
    int? _fixedGameHeight;

    void SelectBestGameResolution()
    {
        if (MinResolution.x == 0 || MaxResolution.x == 0 || Native.x == 0 || Native.y == 0) return;

        const int DOUBLED_RESOLUTION = 2;

        Scale = DOUBLED_RESOLUTION * _ratioCorrection;
        if (_fixedGameHeight is int height)
        {
            // Fixed-size inventory/trade canvases must fit inside the usable
            // screen, including on narrow phones. Map scenes leave this unset.
            int usableWidth = _native.x - System.Math.Max(0, NativeSafeAreaInsets.Left) - System.Math.Max(0, NativeSafeAreaInsets.Right);
            int usableHeight = _native.y - System.Math.Max(0, NativeSafeAreaInsets.Top) - System.Math.Max(0, NativeSafeAreaInsets.Bottom);
            if (usableWidth > 0 && usableHeight > 0 && minimumSafeGameSize.x > 0 && minimumSafeGameSize.y > 0)
            {
                double outputScale = System.Math.Min(usableWidth / (minimumSafeGameSize.x * (double)Scale.x),
                    usableHeight / (minimumSafeGameSize.y * (double)Scale.y));
                // A spare logical pixel covers inward rounding of both edges.
                height = System.Math.Max(height, (int)System.Math.Ceiling(_native.y / (outputScale * Scale.y)) + 1);
            }
            OutputScale = _native.y / (height * Scale.y);
            BackBuffer = new Vector2((int)MathF.Round(_native.x / OutputScale),
                (int)MathF.Round(height * Scale.y));
            Game = new Vector2((int)MathF.Round(BackBuffer.x / Scale.x), height);
            return;
        }

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

        Game = (Vector2f)BackBuffer / Scale;
    }
}
