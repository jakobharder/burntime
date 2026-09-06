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
        if (bars.Count == 0)
        {
            font.DrawText(target, center, text, TextAlignment.Center,
                VerticalTextAlignment.Center, alpha);
            return;
        }

        string label = text + " ";
        int labelWidth = font.GetWidth(label);
        int barsWidth = 0;
        for (int i = 0; i < bars.Count; i++)
            barsWidth += GetWidth(bars[i].Type);

        int totalWidth = labelWidth + barsWidth;
        Vector2 position = center - new Vector2(totalWidth / 2, 0);
        position.x = System.Math.Clamp(position.x, 0,
            System.Math.Max(0, target.Size.x - totalWidth));
        font.DrawText(target, position, label, TextAlignment.Left,
            VerticalTextAlignment.Center, alpha);
        position.x += labelWidth;

        for (int i = 0; i < bars.Count; i++)
        {
            GuiTextBar bar = bars[i];
            ISprite sprite = GetSprite(bar);
            target.DrawSprite(new Vector2(position.x, position.y - sprite.Height / 2),
                sprite, alpha);
            position.x += GetWidth(bar.Type);
        }
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
