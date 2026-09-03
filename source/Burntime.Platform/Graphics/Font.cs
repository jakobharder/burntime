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
    public IReadOnlyDictionary<string, int> Kerning { get; private set; } = new Dictionary<string, int>();
    public int Offset { get; private set; }
    public int Height { get; private set; }
    public bool PostFilter { get; private set; }
    public bool IsLoaded { get; private set; }

    public void Load(ISprite sprite, Dictionary<char, CharInfo> charInfo, Dictionary<string, int> kerning,
        int offset, int height, bool postFilter)
    {
        Sprite = sprite;
        CharInfo = charInfo;
        Kerning = kerning;
        Offset = offset;
        Height = height;
        PostFilter = postFilter;
        IsLoaded = true;
    }

    public int Unload()
    {
        if (!IsLoaded)
            return 0;

        int memory = Sprite.Unload();
        IsLoaded = false;
        return memory;
    }
}

public class Font
{
    // Inline text markup: {x blinks x, while {{ renders a literal opening brace.
    const char BlinkMarker = '{';

    readonly record struct ParsedText(string Text, HashSet<int> BlinkingCharacters);

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
        if (!Info.Colorize)
        {
            DrawText(target, position, text, align, verticalAlign,
                new PixelColor((int)(255 * alpha), 255, 255, 255));
        }
        else if (Info.UseBackColor)
        {
            DrawText(target, position, text, align, verticalAlign,
                new PixelColor((int)(255 * alpha), 255, 255, 255));
        }
        else
        {
            var c = new PixelColor((int)(Info.ForeColor.a * alpha), Info.ForeColor.r, Info.ForeColor.g, Info.ForeColor.b);
            DrawText(target, position, text, align, verticalAlign, c);
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
            float lineWidth = GetWidthFPlain(str);
            float renderX = offset.x;

            if (align == TextAlignment.Center)
                renderX -= lineWidth / 2;
            else if (align == TextAlignment.Right)
                renderX -= lineWidth;

            if (verticalAlign == VerticalTextAlignment.Center)
                offset.y -= GetHeight() / 2;
            else if (verticalAlign == VerticalTextAlignment.Bottom)
                offset.y -= GetHeight();

            if (Borders == TextBorders.Window)
            {
                Vector2 lt = new Vector2();
                float right = lt.x + target.Size.x - lineWidth;
                int bottom = lt.y + target.Size.y - GetHeight();
                renderX = System.Math.Max(renderX, lt.x);
                offset.y = System.Math.Max(offset.y, lt.y);
                renderX = System.Math.Min(renderX, right);
                offset.y = System.Math.Min(offset.y, bottom);
            }
            else if (Borders == TextBorders.Screen)
            {
                Vector2 lt = -target.ScreenOffset + 2;
                float right = lt.x + target.ScreenSize.x - lineWidth - 2;
                int bottom = lt.y + target.ScreenSize.y - GetHeight() - 2;
                renderX = System.Math.Max(renderX, lt.x);
                offset.y = System.Math.Max(offset.y, lt.y);
                renderX = System.Math.Min(renderX, right);
                offset.y = System.Math.Min(offset.y, bottom);
            }

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

    static ParsedText ParseText(string text)
    {
        StringBuilder rendered = new(text.Length);
        HashSet<int> blinkingCharacters = new();

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
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

        return new ParsedText(rendered.ToString(), blinkingCharacters);
    }

    int GetKerningOverlap(char previous, char current)
    {
        return Resource.Kerning.TryGetValue($"{previous}{current}", out int amount) ? amount : 0;
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

        str = ParseText(str).Text;
        Rect rc = new Rect(x, y, 0, 0);
        char last = '\n';
        char previous = '\0';
        float width = 0;

        char[] charray = str.ToCharArray();
        foreach (char ch in charray)
        {
            if (last == '\n')
            {
                rc.Height += (int)(GetHeight() - Resource.Offset);
                rc.Width = System.Math.Max(rc.Width, (int)System.Math.Round(width));
                width = 0;
                previous = '\0';
            }

            if (ch != '\n')
            {
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

        return GetWidthFPlain(ParseText(text).Text);
    }

    float GetWidthFPlain(string text)
    {
        float width = 0;
        char previous = '\0';
        char[] charray = text.ToCharArray();
        foreach (char ch in charray)
        {
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
