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
    Vector2 _native = new(1600, 960);

    public Vector2[] GameResolutions
    {
        get => _gameResolutions;
        set
        {
            _gameResolutions = value;
            SelectBestGameResolution();
        }
    }
    Vector2[] _gameResolutions = System.Array.Empty<Vector2>();

    float _verticalCorrection = 1;
    public float VerticalCorrection
    {
        get => _verticalCorrection;
        set
        {
            _verticalCorrection = value;
            SelectBestGameResolution();
        }
    }

    public Vector2f Scale { get; private set; } = new(1, 1);
    public Vector2 Game { get; private set; } = new(800, 480);

    static bool IsCleanZoom(float zoom)
    {
        return zoom == 4 || zoom == 3 || zoom == 2 || zoom == 1 || zoom == 0.5f;
    }

    void SelectBestGameResolution()
    {
        if (_gameResolutions.Length == 0) return;

        float bestRatio = -1000;
        float bestZoom = 1000;
        int bestIndex = 0;
        float realRatio = _native.Ratio;

        for (int i = 0; i < _gameResolutions.Length; i++)
        {
            float ratio = _gameResolutions[i].Ratio / _verticalCorrection;
            float zoom = _native.y / (float)_gameResolutions[i].y;

            // select best ratio
            if (System.Math.Abs(ratio - realRatio) < System.Math.Abs(bestRatio - realRatio))
            {
                bestRatio = ratio;
                bestZoom = zoom;
                bestIndex = i;
            }

            // if more than one resolutions with that ratio is available, choose best size (prefere too big than too small)
            else if (System.Math.Abs(ratio - realRatio) == System.Math.Abs(bestRatio - realRatio) &&
                (IsCleanZoom(zoom) && !IsCleanZoom(bestZoom) || zoom <= 1 && bestZoom < zoom || zoom >= 1 && bestZoom < 1))
            {
                bestZoom = zoom;
                bestIndex = i;
            }
        }

        Game = _gameResolutions[bestIndex];
        Scale = (Vector2f)_native / (Vector2f)Game;
    }
}
