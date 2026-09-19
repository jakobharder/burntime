using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster;

sealed class InputControlLabelRenderer
{
    const int GlyphSourceSize = 22;
    const int GlyphSourceSize2x = GlyphSourceSize * 2;
    const int KeyboardAtlasColumns = 10;
    const int KeyboardAtlasRows = 5;

    // Keep controller glyphs in the same source-pixel coordinate system as
    // highres-font.txt. This cancels the game's non-square ratio correction,
    // so circles and squares retain their intended proportions on screen.
    static readonly Vector2f GlyphResolution = new(1.0f / 1.875f, 1.0f / 2.25f);
    static readonly int GlyphWidth = (int)System.Math.Round(GlyphSourceSize * GlyphResolution.x);
    static readonly int GlyphHeight = (int)System.Math.Round(GlyphSourceSize * GlyphResolution.y);

    readonly GuiFont _font;
    readonly Module _app;
    readonly bool _brackets;
    readonly bool _bracketTextControls;
    readonly PixelColor _glyphTint;
    readonly GuiImage[][] _glyphs = new GuiImage[4][];
    readonly GuiImage[] _keyboardGlyphs =
        new GuiImage[KeyboardAtlasColumns * KeyboardAtlasRows];
    readonly GuiImage _holdGlyph;

    public int HoldGlyphWidth => GlyphWidth;

    public InputControlLabelRenderer(Module app, GuiFont font, bool brackets = true,
        PixelColor? glyphTint = null, bool bracketTextControls = true)
    {
        _app = app;
        _font = font;
        _brackets = brackets;
        _bracketTextControls = bracketTextControls;
        _glyphTint = glyphTint ?? PixelColor.White;
        _holdGlyph = "gfx/ui/input_glyphs_hold.png";
        string[] families = ["xbox", "playstation", "steam", "switch"];
        for (int family = 0; family < families.Length; family++)
        {
            _glyphs[family] = new GuiImage[16];
            for (int i = 0; i < _glyphs[family].Length; i++)
                _glyphs[family][i] =
                    $"pngsheet@gfx/ui/input_glyphs_{families[family]}.png?{i}?{GlyphSourceSize}x{GlyphSourceSize}";
        }
        for (int i = 0; i < _keyboardGlyphs.Length; i++)
            _keyboardGlyphs[i] =
                $"pngsheet@gfx/ui/input_glyphs_keyboard.png?{i}?{GlyphSourceSize}x{GlyphSourceSize}";
    }

    public int Measure(InputControlLabel control, string label = "", string prefix = "",
        bool labelFirst = false)
    {
        bool brackets = UsesBrackets(control);
        int width = _font.GetWidth(prefix);
        if (brackets)
            width += _font.GetWidth("[]");
        foreach (InputControlPart part in control.Parts)
            width += part.HasGlyph ? GlyphWidth : _font.GetWidth(part.Text);
        if (label.Length > 0)
            width += GetLabelGap(control, brackets, labelFirst) +
                _font.GetWidth(label);
        return width;
    }

    public void Draw(RenderTarget target, Vector2 position, InputControlLabel control,
        string label = "", string prefix = "", TextAlignment alignment = TextAlignment.Left,
        bool labelFirst = false)
    {
        bool brackets = UsesBrackets(control);
        int width = Measure(control, label, prefix, labelFirst);
        int x = alignment == TextAlignment.Right ? position.x - width : position.x;
        if (labelFirst && label.Length > 0)
        {
            int gap = GetLabelGap(control, brackets, labelFirst: true);
            int controlX = x + _font.GetWidth(label) + gap;
            _font.DrawText(target, new Vector2(controlX - gap, position.y), label,
                TextAlignment.Right, VerticalTextAlignment.Top);
            x = controlX;
        }
        DrawText(target, ref x, position.y, prefix + (brackets ? "[" : ""));
        foreach (InputControlPart part in control.Parts)
        {
            if (!part.HasGlyph)
            {
                DrawText(target, ref x, position.y, part.Text);
                continue;
            }

            ISprite glyph;
            PixelColor glyphTint = _glyphTint;
            int sourceSize = _app.Engine.OutputFiltering == OutputFiltering.Xbr2
                ? GlyphSourceSize2x
                : GlyphSourceSize;
            if (part.Keyboard != KeyboardGlyph.None)
            {
                glyph = _keyboardGlyphs[GetKeyboardAtlasIndex(part.Keyboard)];
            }
            else
            {
                int index = (int)part.Glyph - 1;
                GamepadLabelStyle labelStyle = _app.Engine.InputGlyphs.LabelStyle;
                int family = (int)labelStyle;
                glyph = _glyphs[family][index];
                if ((labelStyle is GamepadLabelStyle.Xbox or GamepadLabelStyle.PlayStation) &&
                    part.Glyph is >= InputGlyph.FaceSouth and <= InputGlyph.FaceNorth)
                    glyphTint = PixelColor.White;
            }
            DrawGlyph(target, new Vector2(x, position.y), glyph, sourceSize, glyphTint);
            x += GlyphWidth;
        }
        string suffix = brackets ? "]" : "";
        DrawText(target, ref x, position.y, suffix);
        if (!labelFirst && label.Length > 0)
        {
            x += GetLabelGap(control, brackets, labelFirst: false);
            DrawText(target, ref x, position.y, label);
        }
    }

    public void DrawHoldGlyph(RenderTarget target, Vector2 position)
    {
        int sourceSize = _app.Engine.OutputFiltering == OutputFiltering.Xbr2
            ? GlyphSourceSize2x
            : GlyphSourceSize;
        DrawGlyph(target, position, _holdGlyph, sourceSize, _glyphTint);
    }

    void DrawGlyph(RenderTarget target, Vector2 position, ISprite glyph, int sourceSize,
        PixelColor tint)
    {
        if (glyph.Touch())
            glyph.Resolution = GlyphResolution * GlyphSourceSize / sourceSize;
        target.SelectSprite(glyph);
        target.DrawSelectedSpriteF(
            new Vector2f(position.x,
                position.y + (_font.GetHeight() - GlyphHeight) / 2 +
                (_app.IsNewGfx ? 0.5f : 0)),
            new Rect(Vector2.Zero, new Vector2(sourceSize, sourceSize)),
            tint,
            postFilter: true, directToFramebuffer: !_app.IsNewGfx);
    }

    static int GetKeyboardAtlasIndex(KeyboardGlyph glyph)
    {
        int sequentialIndex = (int)glyph - 1;
        if (glyph is >= KeyboardGlyph.A and <= KeyboardGlyph.Space)
            return sequentialIndex / 8 * KeyboardAtlasColumns + sequentialIndex % 8;

        return glyph switch
        {
            KeyboardGlyph.Ctrl => 8,
            KeyboardGlyph.Left => 9,
            KeyboardGlyph.Shift => 18,
            KeyboardGlyph.Right => 19,
            KeyboardGlyph.Alt => 28,
            KeyboardGlyph.MouseLeft => 29,
            KeyboardGlyph.Up => 38,
            KeyboardGlyph.MouseRight => 39,
            KeyboardGlyph.Down => 48,
            _ => 0
        };
    }

    bool UsesBrackets(InputControlLabel control)
    {
        if (_brackets)
            return true;
        foreach (InputControlPart part in control.Parts)
            if (part.HasGlyph)
                return false;
        return _bracketTextControls;
    }

    int GetLabelGap(InputControlLabel control, bool brackets, bool labelFirst = false)
    {
        bool touchesGlyph = !brackets && control.Parts.Count > 0 &&
            (labelFirst ? control.Parts[0] : control.Parts[^1]).HasGlyph;
        int spaceWidth = _font.GetWidth(" ");
        return touchesGlyph ? System.Math.Max(1, spaceWidth / 2) : spaceWidth;
    }

    void DrawText(RenderTarget target, ref int x, int y, string text)
    {
        _font.DrawText(target, new Vector2(x, y), text,
            TextAlignment.Left, VerticalTextAlignment.Top);
        x += _font.GetWidth(text);
    }
}
