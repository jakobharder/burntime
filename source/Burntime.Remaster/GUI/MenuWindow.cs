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
        public InputShortcut Shortcut;
    }

    public class MenuWindow : Window
    {
        public Func<InputAction, bool>? ShortcutAction { get; set; }
        public Func<InputAction, float, bool>? HeldShortcutAction { get; set; }

        readonly List<MenuItem> _menuEntries;
        readonly GuiImage _topElement;
        readonly GuiImage _middleElement;
        readonly GuiImage _bottomElement;
        readonly GuiFont _defaultFont;
        readonly GuiFont _focusFont;
        readonly GuiFont _promptFont;
        readonly InputControlRenderer _defaultControlRenderer;
        readonly InputControlRenderer _focusControlRenderer;

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
            _promptFont = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray)
            {
                Borders = TextBorders.None
            };
            _defaultControlRenderer = new InputControlRenderer(app, _defaultFont, brackets: false);
            _focusControlRenderer = new InputControlRenderer(app, _focusFont, brackets: false);

            _focusIndex = -1;
            IsModal = true;
            HasFocus = true;
            CaptureAllMouseClicks = true;
        }


        public void AddLine(GuiString text, CommandEvent command,
            InputShortcut shortcut = default)
        {
            MenuItem item;
            item.Text = text;
            item.Command = command;
            item.Shortcut = shortcut;
            _menuEntries.Add(item);
        }

        public void AddLine(int position, GuiString text, CommandEvent command)
        {
            MenuItem item;
            item.Text = text;
            item.Command = command;
            item.Shortcut = default;
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

        public InputAction AlternatePrimaryAction { get; set; }
        public float? ExternalPromptLayer { get; set; }

        public override void OnRender(RenderTarget target)
        {
            target.DrawSprite(Vector2.Zero, _topElement);

            InputControlLabel alternatePrimaryControl = InputControlLabel.Empty;
            if (AlternatePrimaryAction != InputAction.None &&
                app.LastInputMode is (InputMode.Keyboard or InputMode.Gamepad) &&
                (app is not BurntimeClassic classic || classic.ShowInputPrompts))
            {
                alternatePrimaryControl = InputControlDisplay.Resolve(app,
                    app.LastInputMode, AlternatePrimaryAction);
            }
            InputMode shortcutInputMode = app.LastInputMode == InputMode.Gamepad
                ? InputMode.Gamepad
                : InputMode.Keyboard;
            bool showShortcuts = app is not BurntimeClassic promptOwner ||
                promptOwner.ShowInputPrompts;

            for (int i = 0; i < _menuEntries.Count; i++)
            {
                int itemx = 0;
                int itemy = 4 + 11 * i;
                int textWidth = _defaultFont.GetWidth(_menuEntries[i].Text);
                int textx = 34 - textWidth / 2;
                int texty = itemy + 2;

                target.DrawSprite(new Vector2(itemx, itemy), _middleElement);
                target.Layer++;

                GuiFont f = _focusIndex == i ? _focusFont : _defaultFont;
                InputControlRenderer renderer = _focusIndex == i
                    ? _focusControlRenderer
                    : _defaultControlRenderer;
                if (i == 0 && !alternatePrimaryControl.IsEmpty)
                {
                    const int controlGap = 2;
                    int controlWidth = renderer.Measure(alternatePrimaryControl);
                    renderer.Draw(target,
                        new Vector2(textx - controlGap - controlWidth, texty),
                        alternatePrimaryControl);
                }

                InputShortcut shortcut = _menuEntries[i].Shortcut;
                if (showShortcuts && shortcut.Action != InputAction.None)
                {
                    InputControlLabel shortcutControl = InputControlDisplay.Resolve(app,
                        shortcutInputMode, shortcut.Action,
                        shortcut.PreferredKeyboardControl,
                        shortcut.PreferredGamepadControl,
                        shortcut.KeyboardOverride, shortcut.GamepadOverride);
                    if (!shortcutControl.IsEmpty)
                    {
                        float rowLayer = target.Layer;
                        if (ExternalPromptLayer.HasValue)
                            target.Layer = ExternalPromptLayer.Value;

                        const int backgroundRightPadding = 2;
                        int shortcutX = Size.x + 2;
                        string holdText = shortcut.Hold
                            ? InputControlDisplay.Localized(app,
                                InputControlDisplay.Hold) + " "
                            : string.Empty;
                        int holdWidth = _promptFont.GetWidth(holdText);
                        int shortcutWidth = renderer.Measure(shortcutControl);
                        int backgroundX = Size.x - 1;
                        target.RenderRect(
                            new Vector2(backgroundX, itemy),
                            new Vector2(shortcutX - backgroundX + holdWidth +
                                shortcutWidth + backgroundRightPadding, MIDDLE_HEIGHT),
                            new PixelColor(128, 0, 0, 0));
                        if (shortcut.Hold)
                        {
                            _promptFont.DrawText(target,
                                new Vector2(shortcutX, texty), holdText,
                                TextAlignment.Left, VerticalTextAlignment.Top);
                            shortcutX += holdWidth;
                        }
                        renderer.Draw(target, new Vector2(shortcutX, texty),
                            shortcutControl);

                        target.Layer = rowLayer;
                    }
                }

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

            if (action == InputAction.Primary ||
                AlternatePrimaryAction != InputAction.None && action == AlternatePrimaryAction)
            {
                if (_focusIndex < 0 && _menuEntries.Count > 0)
                    _focusIndex = 0;
                else if (_focusIndex >= 0 && _focusIndex < _menuEntries.Count)
                    Execute(_focusIndex);
                return true;
            }

            return ShortcutAction?.Invoke(action) == true;
        }

        public override bool OnHeldInputAction(InputAction action, float elapsed) =>
            HeldShortcutAction?.Invoke(action, elapsed) == true;

        protected override bool UseGamepadDPadNavigation => ShortcutAction == null;

        public override InputAction ResolveInputAction(InputAction action) =>
            ShortcutAction == null ? base.ResolveInputAction(action) : action;

        void Execute(int index)
        {
            Hide();
            _menuEntries[index].Command?.Execute();
        }
    }
}
