
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Classic.GUI
{
    public struct MenuItem
    {
        public String Text;
        public CommandEvent Command;
    }

    public class MenuWindow : Window
    {
        List<MenuItem> list;
        GuiImage top;
        GuiImage middle;
        GuiImage bottom;
        GuiFont font;
        GuiFont hoverFont;

        public MenuWindow(Module App)
            : base(App)
        {
            top = "munt.raw?24";
            middle = "munt.raw?25";
            bottom = "munt.raw?26";

            list = new List<MenuItem>();

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(108, 116, 168));
            font.Borders = TextBorders.Screen;
            hoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56));
            hoverFont.Borders = TextBorders.Screen;

            hover = -1;
            IsModal = true;
            CaptureAllMouseClicks = true;
        }


        public void AddLine(GuiString text, CommandEvent command)
        {
            MenuItem item;
            item.Text = text;
            item.Command = command;
            list.Add(item);
        }

        public void AddLine(int position, GuiString text, CommandEvent command)
        {
            MenuItem item;
            item.Text = text;
            item.Command = command;
            list.Insert(position, item);
        }

        public void RemoveLine(int Position)
        {
            list.RemoveAt(Position);
        }

        public void Clear()
        {
            list.Clear();
        }

        public void Show(Vector2 Position, Nullable<Rect> Boundings)
        {
            this.Position = Position;
            Size = new Vector2(68, 10 + 11 * list.Count);
            this.Position -= this.Boundings.Size / 2;

            if (Boundings.HasValue)
                MoveInside(Boundings.Value);

            Show();
        }

        int hover;

        public override void OnRender(RenderTarget target)
        {
            target.DrawSprite(Vector2.Zero, top);

            for (int i = 0; i < list.Count; i++)
            {
                int itemx = 0;
                int itemy = 4 + 11 * i;
                int textx = 34 - font.GetWidth(list[i].Text) / 2;
                int texty = itemy + 2;

                target.DrawSprite(new Vector2(itemx, itemy), middle);
                target.Layer++;

                GuiFont f = (hover == i) ? hoverFont : font;
                f.DrawText(target, new Vector2(textx, texty), list[i].Text, TextAlignment.Left, VerticalTextAlignment.Top);
                target.Layer--;
            }

            target.DrawSprite(new Vector2(0, top.Height + middle.Height * list.Count), bottom);
        }

        public override void OnMouseLeave()
        {
            hover = -1;
        }

        public override bool OnMouseMove(Vector2 Position)
        {
            hover = -1;

            int itemtop = Position.y - top.Height;
            int itemleft = Position.x;

            if (itemtop >= 0)
            {
                int item = (itemtop - itemtop % middle.Height) / middle.Height;
                if (item < list.Count)
                {
                    int w = font.GetWidth(list[item].Text);

                    if ((itemleft >= middle.Width / 2 - w / 2) && (itemleft < middle.Width / 2 + w / 2))
                    {
                        hover = item;
                    }
                }
            }

            return true;
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            if (Boundings.PointInside(this.Position + Position))
            {
                if (hover >= 0 && hover < list.Count && Button == MouseButton.Left)
                {
                    Hide();

                    if (list[hover].Command != null)
                    {
                        list[hover].Command.Execute();
                    }
                }
                return true;
            }

            if (Button == MouseButton.Left)
                Hide();

            return true;
        }
    }
}
