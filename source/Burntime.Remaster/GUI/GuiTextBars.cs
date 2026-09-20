using System.Collections.Generic;
using System.Linq;
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
    readonly IResourceManager resourceManager;
    readonly Module app;

    public GuiTextBars(Module app)
    {
        this.app = app;
        resourceManager = app.ResourceManager;
        this.resourceManager = resourceManager;
    }

    public void Draw(RenderTarget target, Vector2 center, string text, PixelColor color,
        float alpha, IReadOnlyList<GuiTextBar> bars, string? leadingIcon = null,
        ISprite? marker = null, ISprite? trailingMarker = null,
        bool showBackground = false)
    {
        if (app is BurntimeClassic classic && !classic.ShowUIHints)
        {
            bars = System.Array.Empty<GuiTextBar>();
            leadingIcon = null;
            trailingMarker = null;
        }

        Font font = resourceManager.GetFont(BurntimeClassic.FontName, color);
        List<FontInlineRun> runs = new(4 +
            (leadingIcon != null ? 1 : 0) + (marker != null ? 2 : 1) +
            (trailingMarker != null ? 1 : 0));
        if (leadingIcon != null)
            runs.Add(new FontInlineRun(leadingIcon));
        runs.Add(new FontInlineRun(marker != null || bars.Count > 0 ? text + " " : text));
        if (marker != null)
            runs.Add(new FontInlineRun(marker,
                marker.Width + (bars.Count > 0 ? 1 : 0), verticalOffset: 2));
        if (bars.Count > 0)
            runs.Add(new FontInlineRun(string.Concat(bars.Select(GetIndicatorText))));
        if (trailingMarker != null)
            runs.Add(new FontInlineRun(trailingMarker, trailingMarker.Width));

        font.DrawInline(target, center, runs, TextAlignment.Center,
            VerticalTextAlignment.Center, alpha,
            backgroundColor: showBackground && app is BurntimeClassic { ShowUIHints: true }
                ? new PixelColor((int)(128 * alpha), 0, 0, 0)
                : null,
            // The marker frames contain two transparent columns before the icon.
            backgroundPaddingLeft: leadingIcon != null ? 0 : 2,
            backgroundPaddingRight: 1, backgroundPaddingVertical: 1);
    }

    static string GetIndicatorText(GuiTextBar bar) =>
        $"~{bar.Type switch
        {
            GuiTextBarType.RedBar => 'r',
            GuiTextBarType.Dots => 'd',
            _ => 'b'
        }}{bar.Value}";
}
