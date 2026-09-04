using Burntime.Platform.Graphics;
using Burntime.Platform.Utils;
namespace Burntime.Platform;

public interface ILoadingCounter
{
    void IncreaseLoadingCount();
    void DecreaseLoadingCount();
}

public interface IEngine
{
    DeviceManager DeviceManager { get; set; }
    float Layer { get; set; }
    float MaxLayers { get; }

    BlendOverlayBase BlendOverlay { get; }
    Resolution Resolution { get; }
    RenderTarget MainTarget { get; }
    IMusic Music { get; }
    IInputGlyphProvider InputGlyphs { get; }
    ControllerGlyphMode ControllerGlyphMode { get; set; }
    string AutomaticLanguage { get; }

    bool MusicBlend { get; set; }
    bool IsLoading { get; set; }
    bool SupportsFullscreenToggle { get; }
    bool IsFullscreen { get; set; }
    OutputFiltering OutputFiltering { get; set; }
    bool ForceLinearOutputFiltering { get; }
    bool ForceNearestPointOutputFiltering { get; }
    bool DisableShaders { get; }
    bool SupportsSharpBilinearShader { get; }
    bool SupportsXbr2Shader { get; }
    float Xbr2IndividualLayer { set; }

    void CenterMouse();
    void ExitApplication();

    void ReloadGraphics();

    void RenderRect(Vector2 pos, Vector2 size, PixelColor color, bool postFilter = false);
    void RenderLine(Vector2 start, Vector2 end, PixelColor color);

    void RenderSprite(ISprite sprite, Vector2 pos, float alpha = 1);
    void RenderSprite(ISprite sprite, Vector2 pos, Vector2 srcPos, int srcWidth, int srcHeight, PixelColor color);
    void RenderSpriteF(ISprite sprite, Vector2f pos, Vector2 srcPos, int srcWidth,
        int srcHeight, PixelColor color, bool postFilter = false,
        bool directToFramebuffer = false);
}
