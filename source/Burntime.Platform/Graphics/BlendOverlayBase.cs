using System.Threading;

namespace Burntime.Platform.Graphics;

public abstract class BlendOverlayBase
{
    protected readonly FadingHelper _blendFade = new(2.5f);
    protected Vector2 _size;

    public bool BlockFadeOut { get; set; }
    public bool Block { get; set; }

    public bool IsBlended => _blendFade.State == 1;

    public float Speed
    {
        get => _blendFade.Speed;
        set => _blendFade.Speed = value;
    }
    public float BlendState => _blendFade.State;

    public BlendOverlayBase(Vector2 ScreenSize)
    {
        _size = ScreenSize;
    }

    /// <summary>
    /// Fade out and optionally block current thread until the overlay is completely drawn.
    /// </summary>
    /// <param name="wait">Block current thread</param>
    public void FadeOut(bool wait = false)
    {
        _blendFade.FadeTo = 1;

        if (wait)
            Thread.Sleep(500 + (int)((1 - _blendFade.State) * 1000 / _blendFade.Speed));
    }

    /// <summary>
    /// Fade in and block current thread until the overlay has completely disappeared.
    /// </summary>
    public void FadeIn()
    {
        if (_blendFade.State != 1)
            Thread.Sleep((int)((1 - _blendFade.State) * 1000 / _blendFade.Speed));

        _blendFade.FadeTo = 0;
    }
}
