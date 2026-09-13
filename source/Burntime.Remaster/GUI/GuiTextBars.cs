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
    const int CounterFrameCount = 15;
    const int RedRowOffset = CounterFrameCount;
    const int TrapRowOffset = CounterFrameCount * 2;

    const int BarWidth = 4;
    const int DotsWidth = 2;

    readonly IResourceManager resourceManager;
    readonly ISprite[] counterSprites = new ISprite[CounterFrameCount * 3];

    public GuiTextBars(IResourceManager resourceManager)
    {
        this.resourceManager = resourceManager;
        for (int frame = 0; frame < counterSprites.Length; frame++)
        {
            ISprite sprite = resourceManager.GetImage(
                $"pngsheet@gfx/ui/info_counter.png?{frame}?8x12");
            sprite.Touch();
            counterSprites[frame] = sprite;
        }
    }

    public void Draw(RenderTarget target, Vector2 center, string text, PixelColor color,
        float alpha, IReadOnlyList<GuiTextBar> bars)
    {
        Font font = resourceManager.GetFont(BurntimeClassic.FontName, color);
        List<FontInlineRun> runs = new(bars.Count + 1)
        {
            new FontInlineRun(bars.Count > 0 ? text + " " : text)
        };
        for (int i = 0; i < bars.Count; i++)
        {
            GuiTextBar bar = bars[i];
            runs.Add(new FontInlineRun(GetSprite(bar), GetWidth(bar.Type)));
        }
        font.DrawInline(target, center, runs, TextAlignment.Center,
            VerticalTextAlignment.Center, alpha);
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
