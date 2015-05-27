/*
 *  Burntime Platform
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform.Graphics
{
    public struct FontInfo
    {
        public String Font;
        public PixelColor ForeColor;
        public PixelColor BackColor;
        public bool UseBackColor;
    }

    public enum TextAlignment
    {
        Left,
        Center,
        Right
    }

    public enum VerticalTextAlignment
    {
        Top,
        Center,
        Bottom
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

        internal Sprite sprite;
        internal Dictionary<char, CharInfo> charInfo;
        internal int offset;
        internal int height;

        TextBorders borders = TextBorders.Window;
        public TextBorders Borders
        {
            get { return borders; }
            set { borders = value; }
        }

        Engine engine;
        public Font(Engine Engine)
        {
            engine = Engine;
        }

        public void DrawText(RenderTarget Target, Vector2 Position, String Text)
        {
            DrawText(Target, Position, Text, TextAlignment.Left);
        }

        public void DrawText(RenderTarget Target, Vector2 Position, String Text, TextAlignment Align)
        {
            DrawText(Target, Position, Text, Align, VerticalTextAlignment.Center);
        }

        public void DrawText(RenderTarget Target, Vector2 Position, String Text, TextAlignment Align, VerticalTextAlignment VertAlign)
        {
            DrawText(Target, Position, Text, Align, VertAlign, 1);
        }

        public void DrawText(RenderTarget Target, Vector2 Position, String Text, TextAlignment Align, VerticalTextAlignment VertAlign, float alpha)
        {
            Target.Layer++;
            if (Info.UseBackColor)
            {
                DrawText(Target, Position, Text, Align, VertAlign, new PixelColor((int)(255 * alpha), 255, 255, 255));
            }
            else
            {
                PixelColor c = new PixelColor((int)(Info.ForeColor.a * alpha), Info.ForeColor.r, Info.ForeColor.g, Info.ForeColor.b);
                DrawText(Target, Position, Text, Align, VertAlign, c);
            }
            Target.Layer--;
        }

        void DrawText(RenderTarget target, Vector2 pos, String text, TextAlignment Align, VerticalTextAlignment VertAlign, PixelColor color)
        {
            // TODO: text align
            if (text == null || text.Length == 0)
                return;

            Vector2 offset = new Vector2(pos);

            string[] lines = text.Split('\n');
            foreach (string str in lines)
            {
                if (str.Length == 0)
                    continue;

                offset.x = pos.x;

                if (Align == TextAlignment.Center)
                    offset.x -= GetRect(0, 0, str).Width / 2;
                else if (Align == TextAlignment.Right)
                    offset.x -= GetRect(0, 0, str).Width;

                if (VertAlign == VerticalTextAlignment.Center)
                    offset.y -= GetHeight() / 2;
                else if (VertAlign == VerticalTextAlignment.Bottom)
                    offset.y -= GetHeight();

                if (borders == TextBorders.Window)
                {
                    Vector2 lt = new Vector2();
                    Vector2 rb = lt + target.Size - new Vector2(GetWidth(str), GetHeight());
                    offset.ThresholdLT(lt);
                    offset.ThresholdGT(rb);
                }
                else if (borders == TextBorders.Screen)
                {
                    Vector2 lt = -target.ScreenOffset + 2;
                    Vector2 rb = lt + target.ScreenSize - new Vector2(GetWidth(str), GetHeight()) - 2;
                    offset.ThresholdLT(lt);
                    offset.ThresholdGT(rb);
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
            return (int)((height * sprite.Resolution + offset * 2));
        }

        char translateChar(char ch)
        {
            if (charInfo.ContainsKey(ch))
                return ch;
            return '?';
        }

        public virtual bool IsSupportetCharacter(char ch)
        {
            return charInfo.ContainsKey(ch);
        }
    }
}
