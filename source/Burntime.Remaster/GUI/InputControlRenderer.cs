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
            width += GetLabelGap(control, brackets) + _font.GetWidth(label);
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
            int family = (int)_app.Engine.InputGlyphs.LabelStyle;
            ISprite glyph = _glyphs[family][index];
            if (glyph.Touch())
                glyph.Resolution = GlyphResolution;
            target.SelectSprite(glyph);
            target.DrawSelectedSpriteF(
                new Vector2f(x, position.y + (_font.GetHeight() - GlyphHeight) / 2 +
                    (_app.IsNewGfx ? 0.5f : 0)),
                new Rect(Vector2.Zero, new Vector2(GlyphSourceSize, GlyphSourceSize)),
                PixelColor.White,
                postFilter: true, directToFramebuffer: !_app.IsNewGfx);
            x += GlyphWidth;
        }
        string suffix = brackets ? "]" : "";
        DrawText(target, ref x, position.y, suffix);
        if (label.Length > 0)
        {
            x += GetLabelGap(control, brackets);
            DrawText(target, ref x, position.y, label);
        }
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

    int GetLabelGap(InputControlLabel control, bool brackets)
    {
        bool endsWithGlyph = !brackets && control.Parts.Count > 0 &&
            control.Parts[^1].Glyph != InputGlyph.None;
        int spaceWidth = _font.GetWidth(" ");
        return endsWithGlyph ? System.Math.Max(1, spaceWidth / 2) : spaceWidth;
    }

    void DrawText(RenderTarget target, ref int x, int y, string text)
    {
        _font.DrawText(target, new Vector2(x, y), text,
            TextAlignment.Left, VerticalTextAlignment.Top);
        x += _font.GetWidth(text);
    }
}
