using Burntime.Platform.Resource;
using System.Text;

namespace Burntime.Platform.Graphics;

public struct FontInfo
{
    public String Font;
    public PixelColor ForeColor;
    public PixelColor BackColor;
    public bool Colorize;
    public bool UseBackColor;
}

public enum TextAlignment
{
    Left,
    Center,
    Right,
    Default
}

public enum VerticalTextAlignment
{
    Top,
    Center,
    Bottom,
    Default
}

public enum TextBorders
{
    None,
    Screen,
    Window
}

/// <summary>A text or sprite segment in a single inline font layout.</summary>
public readonly struct FontInlineRun
{
    public string? Text { get; }
    public ISprite? Sprite { get; }
    public int Advance { get; }
    public int VerticalOffset { get; }
    public PixelColor? Color { get; }

    public FontInlineRun(string text, PixelColor? color = null)
    {
        Text = text;
        Sprite = null;
        Advance = 0;
        VerticalOffset = 0;
        Color = color;
    }

    public FontInlineRun(ISprite sprite, int? advance = null, int verticalOffset = 0)
    {
        Text = null;
        Sprite = sprite;
        Advance = advance ?? sprite.Width;
        VerticalOffset = verticalOffset;
        Color = null;
    }
}

public struct CharInfo
{
    public int pos;
    public int width;
    public float renderWidth;
    public int imgWidth;
    public int imgHeight;

    public Vector2 spritePos;
};

public sealed class FontResource
{
    public ISprite Sprite { get; private set; } = null!;
    public IReadOnlyDictionary<char, CharInfo> CharInfo { get; private set; } = new Dictionary<char, CharInfo>();
    public IReadOnlyDictionary<string, float> Kerning { get; private set; } =
        new Dictionary<string, float>();
    public IReadOnlyDictionary<char, FontSpriteResource> Indicators { get; private set; } =
        new Dictionary<char, FontSpriteResource>();
    public IReadOnlyDictionary<char, FontSpriteResource> Icons { get; private set; } =
        new Dictionary<char, FontSpriteResource>();
    public int Offset { get; private set; }
    public int Height { get; private set; }
    public bool PostFilter { get; private set; }
    public bool IsLoaded { get; private set; }
    public bool HasSystemCopy => Sprite?.HasSystemCopy ?? false;

    public bool RestoreSystemCopy()
    {
        if (!HasSystemCopy)
            return false;

        IsLoaded = true;
        return true;
    }

    public void Load(ISprite sprite, Dictionary<char, CharInfo> charInfo,
        Dictionary<string, float> kerning,
        Dictionary<char, FontSpriteResource> indicators,
        Dictionary<char, FontSpriteResource> icons,
        int offset, int height, bool postFilter)
    {
        Sprite = sprite;
        CharInfo = charInfo;
        Kerning = kerning;
        Indicators = indicators;
        Icons = icons;
        Offset = offset;
        Height = height;
        PostFilter = postFilter;
        IsLoaded = true;
    }

    public int Unload()
    {
        int memory = Sprite is null ? 0 : Sprite.Unload();
        IsLoaded = false;
        return memory;
    }
}

public sealed class FontSpriteResource
{
    readonly ISprite[] frames;

    public int Advance { get; }

    public FontSpriteResource(ISprite[] frames, int advance)
    {
        this.frames = frames;
        Advance = advance;
    }

    public ISprite GetFrame(int value) =>
        frames[System.Math.Clamp(value, 0, frames.Length - 1)];
}

public class Font
{
    // Inline text markup: {x blinks x, while {{ renders a literal opening brace.
    // ~ followed by an indicator code and decimal value selects an indicator frame.
    const char BlinkMarker = '{';

    const char IndicatorMarker = '~';
    const char IndicatorPlaceholder = '\ufffc';

    readonly record struct ParsedIndicator(FontSpriteResource Resource, int Value);
    readonly record struct ParsedText(string Text, HashSet<int> BlinkingCharacters,
        Dictionary<int, ParsedIndicator> Indicators);

    public FontInfo Info;

#warning slimdx todo below for parameters were internal
    public FontResource Resource { get; set; } = new();

    public TextBorders Borders { get; set; } = TextBorders.Window;

    public bool IsLoaded => Resource.IsLoaded;
    private ResourceManagerBase _resourceManager;

    public Font(ResourceManagerBase resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public void DrawText(RenderTarget target, Vector2 position, string text, TextAlignment align = TextAlignment.Left, 
        VerticalTextAlignment verticalAlign = VerticalTextAlignment.Center, float alpha = 1)
    {
        if (!IsLoaded)
            _resourceManager.LoadFont(this);

        target.Layer++;
        DrawText(target, position, text, align, verticalAlign, GetDrawColor(alpha));
        target.Layer--;
    }

    /// <summary>
    /// Draws one aligned line containing text and sprites. Positioning and border
    /// correction use each run's layout advance.
    /// </summary>
    public void DrawInline(RenderTarget target, Vector2 position,
        IReadOnlyList<FontInlineRun> runs, TextAlignment align = TextAlignment.Left,
        VerticalTextAlignment verticalAlign = VerticalTextAlignment.Center, float alpha = 1,
        PixelColor? backgroundColor = null, int backgroundPaddingLeft = 0,
        int backgroundPaddingRight = 0, int backgroundPaddingVertical = 0)
    {
        if (!IsLoaded)
            _resourceManager.LoadFont(this);
        if (runs == null || runs.Count == 0)
            return;

        float advance = 0;
        int height = GetHeight();
        for (int i = 0; i < runs.Count; i++)
        {
            FontInlineRun run = runs[i];
            if (run.Text != null)
                advance += (int)System.Math.Ceiling(GetWidthF(run.Text));
            else if (run.Sprite != null)
                advance += run.Advance;
        }
        float width = advance;

        Vector2f origin = ResolvePosition(target, position, width, height, align, verticalAlign);
        int layoutLeft = (int)System.Math.Round(origin.x);
        origin.x = layoutLeft;
        if (backgroundColor is PixelColor background)
        {
            int left = layoutLeft - backgroundPaddingLeft;
            int right = layoutLeft + (int)System.Math.Ceiling(width) +
                backgroundPaddingRight;
            int top = (int)System.Math.Floor(origin.y) - backgroundPaddingVertical;
            int bottom = (int)System.Math.Ceiling(origin.y + height) +
                backgroundPaddingVertical;
            target.RenderRect(new Vector2(left, top),
                new Vector2(right - left, bottom - top), background,
                postFilter: Resource.PostFilter);
        }
        float cursor = origin.x;
        PixelColor color = GetDrawColor(alpha);
        target.Layer++;
        for (int i = 0; i < runs.Count; i++)
        {
            FontInlineRun run = runs[i];
            if (run.Text != null)
            {
                PixelColor runColor = run.Color is PixelColor overrideColor
                    ? new PixelColor((int)(overrideColor.a * alpha), overrideColor.r,
                        overrideColor.g, overrideColor.b)
                    : color;
                DrawInlineText(target, new Vector2f(cursor,
                    origin.y + (height - GetHeight()) / 2f), run.Text, runColor);
                cursor += GetWidthF(run.Text);
            }
            else if (run.Sprite != null)
            {
                float spriteY = origin.y +
                    (height - run.Sprite.Height) / 2f + run.VerticalOffset;
                Vector2f spritePosition = new(cursor, spriteY);
                if (Resource.Sprite.LinearFiltering)
                    spritePosition = target.SnapToPhysicalPixels(spritePosition);
                target.DrawSpriteF(spritePosition,
                    run.Sprite, alpha, postFilter: Resource.PostFilter);
                cursor += run.Advance;
            }
        }
        target.Layer--;
    }

    void DrawText(RenderTarget target, Vector2 position, string text, TextAlignment align,
        VerticalTextAlignment verticalAlign, PixelColor color)
    {
        // TODO: text align
        if (text == null || text.Length == 0)
            return;

        ParsedText parsed = ParseText(text);
        Vector2 offset = new Vector2(position);

        string[] lines = parsed.Text.Split('\n');
        int characterIndex = 0;
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string str = lines[lineIndex];
            if (str.Length == 0)
            {
                if (lineIndex < lines.Length - 1)
                    characterIndex++;
                continue;
            }

            offset.x = position.x;
            float lineWidth = GetWidthFPlain(str, parsed, characterIndex);
            Vector2f resolved = ResolvePosition(target, offset, lineWidth, GetHeight(),
                align, verticalAlign);
            float renderX = resolved.x;
            offset.y = (int)resolved.y;

            target.SelectSprite(Resource.Sprite);

            float renderY = offset.y;
            if (Resource.Sprite.LinearFiltering)
            {
                Vector2f snapped = target.SnapToPhysicalPixels(new Vector2f(renderX, renderY));
                renderX = snapped.x;
                renderY = snapped.y;
            }

            char previous = '\0';
            char[] charray = str.ToCharArray();
            foreach (char ch in charray)
            {
                if (parsed.Indicators.TryGetValue(characterIndex,
                    out ParsedIndicator indicator))
                {
                    renderX += DrawIndicator(target, indicator,
                        new Vector2f(renderX, renderY), color.a / 255f);
                    previous = '\0';
                    characterIndex++;
                    continue;
                }
                char current = translateChar(ch);
                float kerningOffset = previous == '\0' ? 0 : GetKerningOverlap(previous, current);
                renderX += kerningOffset;
                if (parsed.BlinkingCharacters.Contains(characterIndex) && target.TotalElapsed % 1 >= 0.5f)
                    renderX += Resource.CharInfo[current].renderWidth;
                else
                    renderX += DrawChar(target, current, new Vector2f(renderX, renderY), color);
                if (!char.IsWhiteSpace(ch))
                    previous = current;
                characterIndex++;
            }

            offset.y += (int)(GetHeight() - Resource.Offset);
            if (lineIndex < lines.Length - 1)
                characterIndex++;
        }
    }

    Vector2f ResolvePosition(RenderTarget target, Vector2 position, float width, int height,
        TextAlignment align, VerticalTextAlignment verticalAlign)
    {
        float x = position.x;
        float y = position.y;
        if (align == TextAlignment.Center)
            x -= width / 2;
        else if (align == TextAlignment.Right)
            x -= width;
        if (verticalAlign == VerticalTextAlignment.Center)
            y -= height / 2f;
        else if (verticalAlign == VerticalTextAlignment.Bottom)
            y -= height;

        if (Borders == TextBorders.Window)
        {
            x = System.Math.Clamp(x, 0, System.Math.Max(0, target.Size.x - width));
            y = System.Math.Clamp(y, 0, System.Math.Max(0, target.Size.y - height));
        }
        else if (Borders == TextBorders.Screen)
        {
            Vector2 topLeft = -target.ScreenOffset + 2;
            float right = topLeft.x + target.ScreenSize.x - width - 2;
            float bottom = topLeft.y + target.ScreenSize.y - height - 2;
            x = System.Math.Clamp(x, topLeft.x, System.Math.Max(topLeft.x, right));
            y = System.Math.Clamp(y, topLeft.y, System.Math.Max(topLeft.y, bottom));
        }
        return new Vector2f(x, y);
    }

    PixelColor GetDrawColor(float alpha)
    {
        if (!Info.Colorize || Info.UseBackColor)
            return new PixelColor((int)(255 * alpha), 255, 255, 255);
        return new PixelColor((int)(Info.ForeColor.a * alpha),
            Info.ForeColor.r, Info.ForeColor.g, Info.ForeColor.b);
    }

    void DrawInlineText(RenderTarget target, Vector2f position, string text, PixelColor color)
    {
        ParsedText parsed = ParseText(text);
        target.SelectSprite(Resource.Sprite);
        float renderX = position.x;
        float renderY = position.y;
        if (Resource.Sprite.LinearFiltering)
        {
            Vector2f snapped = target.SnapToPhysicalPixels(position);
            renderX = snapped.x;
            renderY = snapped.y;
        }

        char previous = '\0';
        for (int i = 0; i < parsed.Text.Length; i++)
        {
            char ch = parsed.Text[i];
            if (ch == '\n')
                continue;
            if (parsed.Indicators.TryGetValue(i, out ParsedIndicator indicator))
            {
                renderX += DrawIndicator(target, indicator,
                    new Vector2f(renderX, renderY), color.a / 255f);
                previous = '\0';
                continue;
            }
            char current = translateChar(ch);
            if (previous != '\0')
                renderX += GetKerningOverlap(previous, current);
            if (parsed.BlinkingCharacters.Contains(i) && target.TotalElapsed % 1 >= 0.5f)
                renderX += Resource.CharInfo[current].renderWidth;
            else
                renderX += DrawChar(target, current, new Vector2f(renderX, renderY), color);
            if (!char.IsWhiteSpace(ch))
                previous = current;
        }
    }

    ParsedText ParseText(string text)
    {
        StringBuilder rendered = new(text.Length);
        HashSet<int> blinkingCharacters = new();
        Dictionary<int, ParsedIndicator> indicators = new();

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            if (Resource.Icons.TryGetValue(current, out FontSpriteResource? icon))
            {
                indicators[rendered.Length] = new ParsedIndicator(icon, 0);
                rendered.Append(IndicatorPlaceholder);
                continue;
            }
            if (current == IndicatorMarker && i + 2 < text.Length &&
                Resource.Indicators.TryGetValue(text[i + 1],
                    out FontSpriteResource? indicator))
            {
                int digit = i + 2;
                int value = 0;
                while (digit < text.Length && char.IsAsciiDigit(text[digit]))
                {
                    int next = text[digit] - '0';
                    value = value > (int.MaxValue - next) / 10
                        ? int.MaxValue
                        : value * 10 + next;
                    digit++;
                }
                if (digit > i + 2)
                {
                    indicators[rendered.Length] = new ParsedIndicator(indicator, value);
                    rendered.Append(IndicatorPlaceholder);
                    i = digit - 1;
                    continue;
                }
            }
            if (current != BlinkMarker)
            {
                rendered.Append(current);
                continue;
            }

            if (++i >= text.Length)
                break;

            char escaped = text[i];
            if (escaped != BlinkMarker)
                blinkingCharacters.Add(rendered.Length);
            rendered.Append(escaped);
        }

        return new ParsedText(rendered.ToString(), blinkingCharacters, indicators);
    }

    float DrawIndicator(RenderTarget target, ParsedIndicator indicator,
        Vector2f position, float alpha)
    {
        ISprite sprite = indicator.Resource.GetFrame(indicator.Value);
        float y = position.y + (GetHeight() - sprite.Height) / 2f;
        Vector2f spritePosition = new(position.x, y);
        if (sprite.LinearFiltering)
            spritePosition = target.SnapToPhysicalPixels(spritePosition);
        target.DrawSpriteF(spritePosition, sprite, alpha, postFilter: Resource.PostFilter);
        return indicator.Resource.Advance;
    }

    float GetKerningOverlap(char previous, char current)
    {
        return Resource.Kerning.TryGetValue($"{previous}{current}", out float amount)
            ? amount
            : 0;
    }

    float DrawChar(RenderTarget target, char ch, Vector2f pos, PixelColor color)
    {
        CharInfo info = Resource.CharInfo[translateChar(ch)];
        target.DrawSelectedSpriteF(pos + new Vector2f(0, Resource.Offset),
            new Rect(info.spritePos, new Vector2(info.imgWidth, info.imgHeight)),
            color, postFilter: Resource.PostFilter);
        return info.renderWidth;
    }

    public Rect GetRect(int x, int y, String str)
    {
        if (!IsLoaded)
            _resourceManager.LoadFont(this);

        ParsedText parsed = ParseText(str);
        str = parsed.Text;
        Rect rc = new Rect(x, y, 0, 0);
        char last = '\n';
        char previous = '\0';
        float width = 0;

        char[] charray = str.ToCharArray();
        for (int characterIndex = 0; characterIndex < charray.Length; characterIndex++)
        {
            char ch = charray[characterIndex];
            if (last == '\n')
            {
                rc.Height += (int)(GetHeight() - Resource.Offset);
                rc.Width = System.Math.Max(rc.Width, (int)System.Math.Round(width));
                width = 0;
                previous = '\0';
            }

            if (ch != '\n')
            {
                if (parsed.Indicators.TryGetValue(characterIndex,
                    out ParsedIndicator indicator))
                {
                    width += indicator.Resource.Advance;
                    previous = '\0';
                    last = ch;
                    continue;
                }
                char current = translateChar(ch);
                CharInfo info = Resource.CharInfo[current];
                if (previous != '\0')
                    width += GetKerningOverlap(previous, current);
                width += info.renderWidth;
                if (!char.IsWhiteSpace(ch))
                    previous = current;
            }

            last = ch;
        }

        rc.Width = System.Math.Max(rc.Width, (int)System.Math.Round(width));

        return rc;
    }

    public int GetWidth(String Text)
    {
        return (int)System.Math.Round(GetWidthF(Text));
    }

    public float GetWidthF(String text)
    {
        if (!IsLoaded)
            _resourceManager.LoadFont(this);

        ParsedText parsed = ParseText(text);
        return GetWidthFPlain(parsed.Text, parsed, 0);
    }

    float GetWidthFPlain(string text, ParsedText? parsed = null, int startIndex = 0)
    {
        float width = 0;
        char previous = '\0';
        char[] charray = text.ToCharArray();
        for (int i = 0; i < charray.Length; i++)
        {
            char ch = charray[i];
            if (parsed is ParsedText parsedText &&
                parsedText.Indicators.TryGetValue(startIndex + i,
                    out ParsedIndicator indicator))
            {
                width += indicator.Resource.Advance;
                previous = '\0';
                continue;
            }
            char current = translateChar(ch);
            CharInfo info = Resource.CharInfo[current];
            if (previous != '\0')
                width += GetKerningOverlap(previous, current);
            width += info.renderWidth;
            if (!char.IsWhiteSpace(ch))
                previous = current;
        }

        return width;
    }

    public virtual int GetHeight()
    {
        return (int)((Resource.Height * Resource.Sprite.Resolution.y + Resource.Offset * 2));
    }

    char translateChar(char ch)
    {
        if (Resource.CharInfo.ContainsKey(ch))
            return ch;
        return '?';
    }

    public virtual bool IsSupportetCharacter(char ch)
    {
        if (!IsLoaded)
            _resourceManager.LoadFont(this);

        return Resource.CharInfo.ContainsKey(ch);
    }
}
