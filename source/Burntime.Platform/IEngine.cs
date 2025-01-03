﻿using Burntime.Platform.Graphics;
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

    bool MusicBlend { get; set; }
    bool IsLoading { get; set; }
    bool IsFullscreen { get; set; }

    void CenterMouse();
    void ExitApplication();

    void ReloadGraphics();

    void RenderRect(Vector2 pos, Vector2 size, PixelColor color);
    void RenderLine(Vector2 start, Vector2 end, PixelColor color);

    void RenderSprite(ISprite sprite, Vector2 pos, float alpha = 1);
    void RenderSprite(ISprite sprite, Vector2 pos, Vector2 srcPos, int srcWidth, int srcHeight, PixelColor color);
}
