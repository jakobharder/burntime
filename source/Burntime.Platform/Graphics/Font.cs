using Burntime.Platform.Resource;

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
    public int imgWidth;
    public int imgHeight;

    public Vector2 spritePos;
};

public class Font
{
    public FontInfo Info;

#warning slimdx todo below for parameters were internal
    public ISprite sprite;
    public Dictionary<char, CharInfo> charInfo;
    public int offset;
    public int height;

    public TextBorders Borders { get; set; } = TextBorders.Window;

    public bool IsLoaded { get; set; }
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
            DrawText(target, position, text, align, verticalAlign, new PixelColor((int)(255 * alpha), 255, 255, 255));
        }
        else if (Info.UseBackColor)
        {
            DrawText(target, position, text, align, verticalAlign, new PixelColor((int)(255 * alpha), 255, 255, 255));
        }
        else
        {
            var c = new PixelColor((int)(Info.ForeColor.a * alpha), Info.ForeColor.r, Info.ForeColor.g, Info.ForeColor.b);
            DrawText(target, position, text, align, verticalAlign, c);
        }
        target.Layer--;
    }

    void DrawText(RenderTarget target, Vector2 position, string text, TextAlignment align, VerticalTextAlignment verticalAlign, PixelColor color)
    {
        // TODO: text align
        if (text == null || text.Length == 0)
            return;

        Vector2 offset = new Vector2(position);

        string[] lines = text.Split('\n');
        foreach (string str in lines)
        {
            if (str.Length == 0)
                continue;

            offset.x = position.x;

            if (align == TextAlignment.Center)
                offset.x -= GetRect(0, 0, str).Width / 2;
            else if (align == TextAlignment.Right)
                offset.x -= GetRect(0, 0, str).Width;

            if (verticalAlign == VerticalTextAlignment.Center)
                offset.y -= GetHeight() / 2;
            else if (verticalAlign == VerticalTextAlignment.Bottom)
                offset.y -= GetHeight();

            if (Borders == TextBorders.Window)
            {
                Vector2 lt = new Vector2();
                Vector2 rb = lt + target.Size - new Vector2(GetWidth(str), GetHeight());
                offset.Min(lt);
                offset.Max(rb);
            }
            else if (Borders == TextBorders.Screen)
            {
                Vector2 lt = -target.ScreenOffset + 2;
                Vector2 rb = lt + target.ScreenSize - new Vector2(GetWidth(str), GetHeight()) - 2;
                offset.Min(lt);
                offset.Max(rb);
            }

            target.SelectSprite(sprite);

            char[] charray = str.ToCharArray();
            foreach (char ch in charray)
            {
                offset.x += DrawChar(target, ch, offset, color);
            }

            offset.y += (int)(GetHeight() - this.offset);
        }
    }

    int DrawChar(RenderTarget target, char ch, Vector2 pos, PixelColor color)
    {
        CharInfo info = charInfo[translateChar(ch)];
        target.DrawSelectedSprite(pos + new Vector2(0, offset), new Rect(info.spritePos, new Vector2(info.imgWidth, info.imgHeight)), color);
        return info.width;
    }

    public Rect GetRect(int x, int y, String str)
    {
        if (!IsLoaded)
            _resourceManager.LoadFont(this);

        Rect rc = new Rect(x, y, 0, 0);
        char last = '\n';
        int width = 0;

        char[] charray = str.ToCharArray();
        foreach (char ch in charray)
        {
            if (last == '\n')
            {
                rc.Height += (int)(GetHeight() - offset);
                rc.Width = System.Math.Max(rc.Width, width);
                width = 0;
            }

            if (ch != '\n')
            {
                CharInfo info = charInfo[translateChar(ch)];
                width += info.width;
            }

            last = ch;
        }

        rc.Width = System.Math.Max(rc.Width, width);

        return rc;
    }

    public int GetWidth(String Text)
    {
        if (!IsLoaded)
            _resourceManager.LoadFont(this);

        int width = 0;
        char[] charray = Text.ToCharArray();
        foreach (char ch in charray)
        {
            CharInfo info = charInfo[translateChar(ch)];
            width += info.width;
        }

        return width;
    }

    public virtual int GetHeight()
    {
        return (int)((height * sprite.Resolution.y + offset * 2));
    }

    char translateChar(char ch)
    {
        if (charInfo.ContainsKey(ch))
            return ch;
        return '?';
    }

    public virtual bool IsSupportetCharacter(char ch)
    {
        if (!IsLoaded)
            _resourceManager.LoadFont(this);

        return charInfo.ContainsKey(ch);
    }
}
