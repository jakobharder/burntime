
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Framework.GUI
{
    public class GuiFont
    {
        protected Font font;

        public GuiFont(ResourceID name, PixelColor foreColor)
        {
            font = Module.Instance.ResourceManager.GetFont(name, foreColor);
        }

        public GuiFont(ResourceID name, PixelColor foreColor, PixelColor backColor)
        {
            font = Module.Instance.ResourceManager.GetFont(name, foreColor, backColor);
        }

        public TextBorders Borders
        {
            get { return font.Borders; }
            set { font.Borders = value; }
        }

        public void DrawText(IRenderTarget target, Vector2 position, string text)
        {
            font.DrawText(target, position, text);
        }

        public void DrawText(IRenderTarget target, Vector2 position, string text, TextAlignment align)
        {
            font.DrawText(target, position, text, align);
        }

        public void DrawText(IRenderTarget target, Vector2 position, string text, TextAlignment align, VerticalTextAlignment vertAlign)
        {
            font.DrawText(target, position, text, align, vertAlign);
        }

        public void DrawText(IRenderTarget target, Vector2 position, string text, TextAlignment align, VerticalTextAlignment vertAlign, float alpha)
        {
            font.DrawText(target, position, text, align, vertAlign, alpha);
        }

        public Rect GetRect(int x, int y, string text)
        {
            return font.GetRect(x, y, text);
        }

        public int GetWidth(string text)
        {
            return font.GetWidth(text);
        }

        public int GetHeight()
        {
            return font.GetHeight();
        }

        public bool IsSupportetCharacter(char ch)
        {
            return font.IsSupportetCharacter(ch);
        }
    }
}
