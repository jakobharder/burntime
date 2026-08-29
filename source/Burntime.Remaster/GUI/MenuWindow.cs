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
        public GuiString Text;
        public CommandEvent Command;
    }

    public class MenuWindow : Window
    {
        readonly List<MenuItem> _menuEntries;
        readonly GuiImage _topElement;
        readonly GuiImage _middleElement;
        readonly GuiImage _bottomElement;
        readonly GuiFont _defaultFont;
        readonly GuiFont _focusFont;

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
            _focusFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56));
            _focusFont.Borders = TextBorders.Screen;

            _focusIndex = -1;
            IsModal = true;
            HasFocus = true;
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

        public void Show(Vector2 Position, Nullable<Rect> Boundings, bool openedByMouse = false)
        {
            this.Position = Position;
            Size = new Vector2(68, 10 + 11 * _menuEntries.Count);
            this.Position -= this.Boundings.Size / 2;

            if (Boundings.HasValue)
                MoveInside(Boundings.Value);

            _lastMousePosition = app.DeviceManager.Mouse.Position - PositionOnScreen;
            _mouseSelectionEnabled = openedByMouse;
            _mouseHasLeft = false;
            _focusIndex = openedByMouse ? GetEntryAt(_lastMousePosition) : _menuEntries.Count > 0 ? 0 : -1;

            Show();
        }

        int _focusIndex;
        Vector2 _lastMousePosition;
        bool _mouseSelectionEnabled;
        bool _mouseHasLeft;

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

                GuiFont f = _focusIndex == i ? _focusFont : _defaultFont;
                f.DrawText(target, new Vector2(textx, texty), _menuEntries[i].Text, TextAlignment.Left, VerticalTextAlignment.Top);
                target.Layer--;
            }

            target.DrawSprite(new Vector2(0, TOP_HEIGHT + MIDDLE_HEIGHT * _menuEntries.Count), _bottomElement);
        }

        public override void OnMouseLeave()
        {
            _focusIndex = -1;
            _mouseHasLeft = true;
        }

        public override bool OnMouseMove(Vector2 Position)
        {
            if (!_mouseHasLeft && (Position - _lastMousePosition).Length <= 1)
                return true;

            _lastMousePosition = Position;
            _mouseSelectionEnabled = true;
            _mouseHasLeft = false;
            _focusIndex = GetEntryAt(Position);
            return true;
        }

        int GetEntryAt(Vector2 position)
        {
            if (!_mouseSelectionEnabled)
                return -1;

            int itemtop = position.y - TOP_HEIGHT;
            int itemleft = position.x;

            if (itemtop >= 0)
            {
                int item = (itemtop - itemtop % MIDDLE_HEIGHT) / MIDDLE_HEIGHT;
                if (item < _menuEntries.Count && item >= 0)
                {
                    int w = _defaultFont.GetWidth(_menuEntries[item].Text);

                    if ((itemleft >= _middleElement.Width / 2 - w / 2) && (itemleft < _middleElement.Width / 2 + w / 2))
                        return item;
                }
            }

            return -1;
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            if (Boundings.PointInside(this.Position + Position))
            {
                if (_focusIndex >= 0 && _focusIndex < _menuEntries.Count && Button == MouseButton.Left)
                {
                    Execute(_focusIndex);
                }
                return true;
            }

            if (Button == MouseButton.Left)
                Hide();

            return true;
        }

        public override bool OnInputAction(InputAction action)
        {
            if (action == InputAction.Back)
            {
                Hide();
                return true;
            }

            if (action.IsUp() || action.IsDown())
            {
                if (_menuEntries.Count == 0)
                    return true;

                int direction = action.IsUp() ? -1 : 1;
                if (_focusIndex < 0)
                    _focusIndex = 0;
                _focusIndex = (_focusIndex + direction + _menuEntries.Count) % _menuEntries.Count;
                return true;
            }

            if (action == InputAction.Primary)
            {
                if (_focusIndex < 0 && _menuEntries.Count > 0)
                    _focusIndex = 0;
                else if (_focusIndex >= 0 && _focusIndex < _menuEntries.Count)
                    Execute(_focusIndex);
                return true;
            }

            return false;
        }

        void Execute(int index)
        {
            Hide();
            _menuEntries[index].Command?.Execute();
        }
    }
}
