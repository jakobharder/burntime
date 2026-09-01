using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster;

sealed class InputControlRenderer
{
    const int GlyphSourceSize = 22;

    // Keep controller glyphs in the same source-pixel coordinate system as
    // highres-font.txt. This cancels the game's non-square ratio correction,
    // so circles and squares retain their intended proportions on screen.
    static readonly Vector2f GlyphResolution = new(1.0f / 1.875f, 1.0f / 2.25f);
    static readonly int GlyphWidth = (int)System.Math.Round(GlyphSourceSize * GlyphResolution.x);
    static readonly int GlyphHeight = (int)System.Math.Round(GlyphSourceSize * GlyphResolution.y);

    readonly GuiFont _font;
    readonly Module _app;
    readonly bool _brackets;
    readonly GuiImage[][] _glyphs = new GuiImage[4][];

    public InputControlRenderer(Module app, GuiFont font, bool brackets = true)
    {
        _app = app;
        _font = font;
        _brackets = brackets;
        string[] families = ["xbox", "playstation", "steam", "switch"];
        for (int family = 0; family < families.Length; family++)
        {
            _glyphs[family] = new GuiImage[16];
            for (int i = 0; i < _glyphs[family].Length; i++)
                _glyphs[family][i] =
                    $"pngsheet@gfx/ui/input_glyphs_{families[family]}.png?{i}?{GlyphSourceSize}x{GlyphSourceSize}";
        }
    }

    public int Measure(InputControlLabel control, string label = "", string prefix = "")
    {
        bool brackets = UsesBrackets(control);
        int width = _font.GetWidth(prefix);
        if (brackets)
            width += _font.GetWidth("[]");
        foreach (InputControlPart part in control.Parts)
            width += part.Glyph == InputGlyph.None ? _font.GetWidth(part.Text) : GlyphWidth;
        if (label.Length > 0)
            width += _font.GetWidth(" " + label);
        return width;
    }

    public void Draw(RenderTarget target, Vector2 position, InputControlLabel control,
        string label = "", string prefix = "", TextAlignment alignment = TextAlignment.Left)
    {
        bool brackets = UsesBrackets(control);
        int width = Measure(control, label, prefix);
        int x = alignment == TextAlignment.Right ? position.x - width : position.x;
        DrawText(target, ref x, position.y, prefix + (brackets ? "[" : ""));
        foreach (InputControlPart part in control.Parts)
        {
            if (part.Glyph == InputGlyph.None)
            {
                DrawText(target, ref x, position.y, part.Text);
                continue;
            }

            int index = (int)part.Glyph - 1;
            ISprite glyph = _glyphs[(int)_app.Engine.InputGlyphs.LabelStyle][index];
            if (glyph.Touch())
                glyph.Resolution = GlyphResolution;
            target.DrawSprite(new Vector2(x, position.y + (_font.GetHeight() - GlyphHeight) / 2), glyph);
            x += GlyphWidth;
        }
        string suffix = brackets ? "]" : "";
        DrawText(target, ref x, position.y,
            label.Length == 0 ? suffix : suffix + " " + label);
    }

    bool UsesBrackets(InputControlLabel control)
    {
        if (_brackets)
            return true;
        foreach (InputControlPart part in control.Parts)
            if (part.Glyph != InputGlyph.None)
                return false;
        return true;
    }

    void DrawText(RenderTarget target, ref int x, int y, string text)
    {
        _font.DrawText(target, new Vector2(x, y), text,
            TextAlignment.Left, VerticalTextAlignment.Top);
        x += _font.GetWidth(text);
    }
}
