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
        readonly InputControlLabelRenderer _defaultControlRenderer;
        readonly InputControlLabelRenderer _focusControlRenderer;

        const int TOP_HEIGHT = 4;
        const int MIDDLE_HEIGHT = 11;
        const int MENU_CONTENT_WIDTH = 68;
        const int MENU_WIDTH = MENU_CONTENT_WIDTH;

        public MenuWindow(Module App)
            : base(App)
        {
            _topElement = "munt.raw?24";
            _middleElement = "munt.raw?25";
            _bottomElement = "munt.raw?26";

            _menuEntries = new List<MenuItem>();

            _defaultFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.MenuText);
            _defaultFont.Borders = TextBorders.Screen;
            _focusFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.MenuTextHover);
            _focusFont.Borders = TextBorders.Screen;
            PixelColor promptColor = ClassicColors.HudTextHover;
            GuiFont promptFont = new(BurntimeClassic.FontName, promptColor)
            {
                Borders = TextBorders.Screen
            };
            _defaultControlRenderer = new InputControlLabelRenderer(app, promptFont,
                brackets: false, glyphTint: promptColor);
            _focusControlRenderer = new InputControlLabelRenderer(app, promptFont,
                brackets: false, glyphTint: promptColor);

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

        public void Show(Vector2 Position, Nullable<Rect> Boundings, bool openedByMouse = false,
            bool heldTouch = false)
        {
            _heldTouch = heldTouch;
            _openedByMouse = openedByMouse;
            this.Position = Position;
            Size = new Vector2(MENU_WIDTH, 10 + MIDDLE_HEIGHT * _menuEntries.Count);
            this.Position -= new Vector2(MENU_CONTENT_WIDTH, Size.y) / 2;

            if (Boundings.HasValue)
                MoveInside(Boundings.Value);

            _lastMousePosition = app.DeviceManager.Mouse.Position - PositionOnScreen;
            bool touchInput = app.LastInputMode == InputMode.Touch;
            _mouseSelectionEnabled = openedByMouse && !touchInput;
            _mouseHasLeft = false;
            _focusIndex = touchInput ? -1 : openedByMouse
                ? GetEntryAt(_lastMousePosition)
                : _menuEntries.Count > 0 ? 0 : -1;
            _touchPressedIndex = -1;
            _touchActivationIndex = -1;
            _touchReleasePending = false;

            Show();
        }

        int _focusIndex;
        Vector2 _lastMousePosition;
        bool _mouseSelectionEnabled;
        bool _mouseHasLeft;
        bool _openedByMouse;
        int _touchPressedIndex = -1;
        int _touchActivationIndex = -1;
        bool _touchReleasePending;
        bool _heldTouch;
        public override bool ContinuesTouchHold => _heldTouch;

        public override void OnTouchHoldMove(Vector2 position)
        {
            if (!_heldTouch) return;
            _focusIndex = GetHeldEntryAt(position);
        }

        public override void OnTouchHoldEnd(Vector2 position, bool cancelled)
        {
            if (!_heldTouch) return;
            _heldTouch = false;
            int index = cancelled ? -1 : GetHeldEntryAt(position);
            if (index >= 0)
                Execute(index);
            else
                Hide();
        }

        int GetHeldEntryAt(Vector2 position)
        {
            if (position.x < 0 || position.x >= Size.x || position.y < TOP_HEIGHT)
                return -1;
            int index = (position.y - TOP_HEIGHT) / MIDDLE_HEIGHT;
            return index < _menuEntries.Count ? index : -1;
        }

        public override bool PreserveMouseModeForDirectionalInput => _openedByMouse;

        public InputAction FirstLineAction { get; set; }
        public float? ExternalPromptLayer { get; set; }

        public override void OnRender(RenderTarget target)
        {
            target.DrawSprite(Vector2.Zero, _topElement);

            InputControlLabel firstLineControl = InputControlLabel.Empty;
            if (FirstLineAction != InputAction.None &&
                app.LastInputMode is (InputMode.Keyboard or InputMode.Gamepad) &&
                (app is not BurntimeClassic classic || classic.ShowUIHints))
            {
                firstLineControl = InputControlDisplay.Resolve(app,
                    app.LastInputMode, FirstLineAction);
            }
            InputMode shortcutInputMode = app.LastInputMode == InputMode.Mouse
                ? InputMode.Keyboard
                : app.LastInputMode;
            bool showShortcuts = app.LastInputMode != InputMode.Touch &&
                (app is not BurntimeClassic promptOwner || promptOwner.ShowUIHints);

            if (showShortcuts && _menuEntries.Exists(
                entry => entry.Shortcut.Action != InputAction.None))
            {
                float menuLayer = target.Layer;
                if (ExternalPromptLayer.HasValue)
                    target.Layer = ExternalPromptLayer.Value;
                else
                    target.Layer++;

                int promptRight = MENU_WIDTH + 2 +
                    _defaultControlRenderer.HoldGlyphWidth;
                int backgroundX = MENU_WIDTH - 1;
                target.RenderRect(
                    new Vector2(backgroundX, TOP_HEIGHT),
                    new Vector2(promptRight - backgroundX + 1,
                        MIDDLE_HEIGHT * _menuEntries.Count + 2),
                    new PixelColor(128, 0, 0, 0));
                target.Layer = menuLayer;
            }

            for (int i = 0; i < _menuEntries.Count; i++)
            {
                int itemx = 0;
                int itemy = 4 + 11 * i;
                int textWidth = _defaultFont.GetWidth(_menuEntries[i].Text);
                int textx = MENU_CONTENT_WIDTH / 2 - textWidth / 2;
                int texty = itemy + 2;

                target.DrawSprite(new Vector2(itemx, itemy), _middleElement);
                target.Layer++;

                GuiFont f = _focusIndex == i ? _focusFont : _defaultFont;
                InputControlLabelRenderer renderer = _focusIndex == i
                    ? _focusControlRenderer
                    : _defaultControlRenderer;
                if (i == 0 && !firstLineControl.IsEmpty)
                {
                    const int controlGap = 2;
                    int controlWidth = renderer.Measure(firstLineControl);
                    int combinedWidth = controlWidth + controlGap + textWidth;
                    int combinedX = (MENU_CONTENT_WIDTH - combinedWidth) / 2;
                    renderer.Draw(target,
                        new Vector2(combinedX, texty),
                        firstLineControl);
                    textx = combinedX + controlWidth + controlGap;
                }

                InputShortcut shortcut = _menuEntries[i].Shortcut;
                if (showShortcuts && shortcut.Action != InputAction.None)
                {
                    Key? preferredKeyboardControl = app.LastInputMode == InputMode.Mouse
                        ? shortcut.PreferredMouseKeyboardControl ??
                            shortcut.PreferredKeyboardControl
                        : shortcut.PreferredKeyboardControl;
                    InputControlLabel shortcutControl = InputControlDisplay.Resolve(app,
                        shortcutInputMode, shortcut.Action,
                        preferredKeyboardControl,
                        shortcut.PreferredGamepadControl);
                    if (!shortcutControl.IsEmpty)
                    {
                        float rowLayer = target.Layer;
                        if (ExternalPromptLayer.HasValue)
                            target.Layer = ExternalPromptLayer.Value;

                        int promptRight = MENU_WIDTH + 2 + renderer.HoldGlyphWidth;
                        if (shortcut.Hold)
                        {
                            int holdX = promptRight -
                                renderer.Measure(shortcutControl) -
                                renderer.HoldGlyphWidth;
                            renderer.DrawHoldGlyph(target,
                                new Vector2(holdX, texty));
                        }
                        renderer.Draw(target, new Vector2(promptRight, texty),
                            shortcutControl, alignment: TextAlignment.Right);

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
            if (_heldTouch) return true;
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

                    if (itemleft >= MENU_CONTENT_WIDTH / 2 - w / 2 &&
                        itemleft < MENU_CONTENT_WIDTH / 2 + w / 2)
                        return item;
                }
            }

            return -1;
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            if (app.LastInputMode == InputMode.Touch)
            {
                _mouseSelectionEnabled = true;
                _focusIndex = _touchReleasePending
                    ? _touchActivationIndex
                    : GetTouchEntryAt(Position);
                _touchReleasePending = false;
            }
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

        public override void OnTouchPress(Vector2 position)
        {
            _touchPressedIndex = GetTouchEntryAt(position);
            _touchActivationIndex = -1;
            _touchReleasePending = false;
            _focusIndex = _touchPressedIndex;
        }

        public override void OnTouchRelease(Vector2 position, bool cancelled)
        {
            int releasedIndex = cancelled ? -1 : GetTouchEntryAt(position);
            _touchActivationIndex = releasedIndex == _touchPressedIndex
                ? releasedIndex
                : -1;
            _touchReleasePending = !cancelled;
            _focusIndex = _touchActivationIndex;
            _touchPressedIndex = -1;
        }

        int GetTouchEntryAt(Vector2 position)
        {
            int selected = -1;
            float distance = float.MaxValue;
            for (int i = 0; i < _menuEntries.Count; i++)
            {
                int width = _defaultFont.GetWidth(_menuEntries[i].Text);
                var bounds = new Rect(MENU_CONTENT_WIDTH / 2 - width / 2,
                    TOP_HEIGHT + i * MIDDLE_HEIGHT, width, MIDDLE_HEIGHT);
                float candidateDistance = (bounds.Center - position).Length;
                if (TouchHitTest.Expand(bounds, TouchHitTest.MinimumSize).PointInside(position) && candidateDistance < distance)
                {
                    selected = i;
                    distance = candidateDistance;
                }
            }
            return selected;
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

            if (FirstLineAction != InputAction.None && action == FirstLineAction)
            {
                if (_menuEntries.Count > 0)
                    Execute(0);
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
