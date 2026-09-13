using System.Collections.Generic;
using Burntime.Framework;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Remaster.GUI;

enum GuiTextBarType
{
    RedBar,
    Dots,
    BlueBar
}

readonly record struct GuiTextBar(GuiTextBarType Type, int Value);

/// <summary>
/// Draws centered map labels followed by compact counter bars.
/// </summary>
sealed class GuiTextBars
{
    static readonly Vector2f FontIconResolution =
        new(1.0f / 1.875f, 1.0f / 2.25f);

    const int CounterFrameCount = 15;
    const int RedRowOffset = CounterFrameCount;
    const int TrapRowOffset = CounterFrameCount * 2;

    const int BarWidth = 4;
    const int DotsWidth = 2;

    readonly IResourceManager resourceManager;
    readonly ISprite[] counterSprites = new ISprite[CounterFrameCount * 3];
    readonly ISprite radiationMarker;
    readonly ISprite gasMarker;
    readonly Module app;

    public GuiTextBars(Module app)
    {
        this.app = app;
        resourceManager = app.ResourceManager;
        this.resourceManager = resourceManager;
        // The sheet is ordered: exclamation, radiation, gas.
        radiationMarker = resourceManager.GetImage("pngsheet@gfx/ui/font_icons.png?1?12x12");
        gasMarker = resourceManager.GetImage("pngsheet@gfx/ui/font_icons.png?2?12x12");
        for (int frame = 0; frame < counterSprites.Length; frame++)
        {
            ISprite sprite = resourceManager.GetImage(
                $"pngsheet@gfx/ui/info_counter.png?{frame}?8x12");
            sprite.Touch();
            counterSprites[frame] = sprite;
        }
    }

    public ISprite RadiationMarker => radiationMarker;
    public ISprite GasMarker => gasMarker;

    public void Draw(RenderTarget target, Vector2 center, string text, PixelColor color,
        float alpha, IReadOnlyList<GuiTextBar> bars, ISprite? leadingMarker = null,
        ISprite? marker = null, ISprite? trailingMarker = null,
        bool showBackground = false)
    {
        UpdateFontIconResolution();

        if (app is BurntimeClassic classic && !classic.ShowUIHints)
        {
            bars = System.Array.Empty<GuiTextBar>();
            leadingMarker = null;
            trailingMarker = null;
        }

        Font font = resourceManager.GetFont(BurntimeClassic.FontName, color);
        List<FontInlineRun> runs = new(bars.Count +
            (leadingMarker != null ? 1 : 0) + (marker != null ? 2 : 1) +
            (trailingMarker != null ? 1 : 0));
        if (leadingMarker != null)
            runs.Add(new FontInlineRun(leadingMarker, leadingMarker.Width + 1));
        runs.Add(new FontInlineRun(marker != null || bars.Count > 0 ? text + " " : text));
        if (marker != null)
            runs.Add(new FontInlineRun(marker,
                marker.Width + (bars.Count > 0 ? 1 : 0), verticalOffset: 2));
        for (int i = 0; i < bars.Count; i++)
        {
            GuiTextBar bar = bars[i];
            runs.Add(new FontInlineRun(GetSprite(bar), GetWidth(bar.Type)));
        }
        if (trailingMarker != null)
            runs.Add(new FontInlineRun(trailingMarker, trailingMarker.Width));

        font.DrawInline(target, center, runs, TextAlignment.Center,
            VerticalTextAlignment.Center, alpha,
            backgroundColor: showBackground && app is BurntimeClassic { ShowUIHints: true }
                ? new PixelColor((int)(128 * alpha), 0, 0, 0)
                : null,
            // The marker frames contain two transparent columns before the icon.
            backgroundPaddingLeft: leadingMarker != null ? 0 : 2,
            backgroundPaddingRight: 1, backgroundPaddingVertical: 1);
    }

    void UpdateFontIconResolution()
    {
        Vector2f resolution = app.IsNewGfx
            ? FontIconResolution
            : Vector2f.One;
        if (app.IsNewGfx && app.Engine.OutputFiltering == OutputFiltering.Xbr2)
            resolution /= 2;

        radiationMarker.Resolution = resolution;
        gasMarker.Resolution = resolution;
    }

    ISprite GetSprite(GuiTextBar bar)
    {
        int value = System.Math.Clamp(bar.Value, 0, CounterFrameCount - 1);
        int frame = bar.Type switch
        {
            GuiTextBarType.RedBar => RedRowOffset + value,
            GuiTextBarType.Dots => TrapRowOffset + value,
            _ => value
        };
        return counterSprites[frame];
    }

    static int GetWidth(GuiTextBarType type) => type switch
    {
        GuiTextBarType.Dots => DotsWidth,
        _ => BarWidth
    };
}
