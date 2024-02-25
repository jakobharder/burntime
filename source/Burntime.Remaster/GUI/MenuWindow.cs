using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Remaster.GUI
{
    public struct MenuItem
    {
        public String Text;
        public CommandEvent Command;
    }

    public class MenuWindow : Window
    {
        readonly List<MenuItem> _menuEntries;
        readonly GuiImage _topElement;
        readonly GuiImage _middleElement;
        readonly GuiImage _bottomElement;
        readonly GuiFont _defaultFont;
        readonly GuiFont _hoverFont;

        const int TOP_HEIGHT = 4;
        const int MIDDLE_HEIGHT = 11;

        public MenuWindow(Module App)
            : base(App)
        {
            _topElement = "munt.raw?24";
            _middleElement = "munt.raw?25";
            _bottomElement = "munt.raw?26";

            _menuEntries = new List<MenuItem>();

            _defaultFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(108, 116, 168));
            _defaultFont.Borders = TextBorders.Screen;
            _hoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56));
            _hoverFont.Borders = TextBorders.Screen;

            hover = -1;
            IsModal = true;
            CaptureAllMouseClicks = true;
        }


        public void AddLine(GuiString text, CommandEvent command)
        {
            MenuItem item;
            item.Text = text;
            item.Command = command;
            _menuEntries.Add(item);
        }

        public void AddLine(int position, GuiString text, CommandEvent command)
        {
            MenuItem item;
            item.Text = text;
            item.Command = command;
            _menuEntries.Insert(position, item);
        }

        public void RemoveLine(int Position)
        {
            _menuEntries.RemoveAt(Position);
        }

        public void Clear()
        {
            _menuEntries.Clear();
        }

        public void Show(Vector2 Position, Nullable<Rect> Boundings)
        {
            this.Position = Position;
            Size = new Vector2(68, 10 + 11 * _menuEntries.Count);
            this.Position -= this.Boundings.Size / 2;

            if (Boundings.HasValue)
                MoveInside(Boundings.Value);

            Show();
        }

        int hover;

        public override void OnRender(RenderTarget target)
        {
            target.DrawSprite(Vector2.Zero, _topElement);

            for (int i = 0; i < _menuEntries.Count; i++)
            {
                int itemx = 0;
                int itemy = 4 + 11 * i;
                int textx = 34 - _defaultFont.GetWidth(_menuEntries[i].Text) / 2;
                int texty = itemy + 2;

                target.DrawSprite(new Vector2(itemx, itemy), _middleElement);
                target.Layer++;

                GuiFont f = (hover == i) ? _hoverFont : _defaultFont;
                f.DrawText(target, new Vector2(textx, texty), _menuEntries[i].Text, TextAlignment.Left, VerticalTextAlignment.Top);
                target.Layer--;
            }

            target.DrawSprite(new Vector2(0, TOP_HEIGHT + MIDDLE_HEIGHT * _menuEntries.Count), _bottomElement);
        }

        public override void OnMouseLeave()
        {
            hover = -1;
        }

        public override bool OnMouseMove(Vector2 Position)
        {
            hover = -1;

            int itemtop = Position.y - TOP_HEIGHT;
            int itemleft = Position.x;

            if (itemtop >= 0)
            {
                int item = (itemtop - itemtop % MIDDLE_HEIGHT) / MIDDLE_HEIGHT;
                if (item < _menuEntries.Count && item >= 0)
                {
                    int w = _defaultFont.GetWidth(_menuEntries[item].Text);

                    if ((itemleft >= _middleElement.Width / 2 - w / 2) && (itemleft < _middleElement.Width / 2 + w / 2))
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
                if (hover >= 0 && hover < _menuEntries.Count && Button == MouseButton.Left)
                {
                    Hide();

                    if (_menuEntries[hover].Command != null)
                    {
                        _menuEntries[hover].Command.Execute();
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
