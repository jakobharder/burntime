using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using System;

namespace Burntime.Remaster;

public readonly record struct InputShortcut(InputAction Action)
{
    public Key? PreferredKeyboardControl { get; init; }
    public GamepadControl? PreferredGamepadControl { get; init; }
    public string? KeyboardOverride { get; init; }
    public string? GamepadOverride { get; init; }
    public bool Hold { get; init; }
}

/// <summary>A non-interactive shortcut column placed beside an existing menu.</summary>
public sealed class InputShortcutColumn : Window
{
    const int HorizontalPadding = 4;
    const int TopHeight = 4;
    const int RowHeight = 11;

    readonly GuiFont _font;
    readonly InputControlRenderer _controlRenderer;
    InputShortcut[] _shortcuts = [];
    ShortcutDisplay[] _display = [];
    InputMode _inputMode = InputMode.None;
    string _language = string.Empty;
    int _glyphRevision = -1;

    readonly record struct ShortcutDisplay(InputControlLabel Control, string Prefix, int Width);

    public PixelColor BackgroundColor { get; set; } = new(128, 0, 0, 0);

    public InputShortcutColumn(Module app)
        : base(app)
    {
        _font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray)
        {
            Borders = TextBorders.None
        };
        _controlRenderer = new InputControlRenderer(app, _font);
        Layer = 200;
    }

    public void SetShortcuts(params InputShortcut[] shortcuts)
    {
        _shortcuts = shortcuts;
        RefreshText();
    }

    public void PlaceBeside(Window anchor, Rect bounds, int gap = 2)
    {
        RefreshInputMode();

        int right = anchor.Boundings.Right + gap;
        if (right + Size.x <= bounds.Right)
        {
            HorizontalAlignment = PositionAlignment.Left;
            Position = new Vector2(right, anchor.Boundings.Top);
        }
        else
        {
            HorizontalAlignment = PositionAlignment.Right;
            Position = new Vector2(anchor.Boundings.Left - gap, anchor.Boundings.Top);
        }
    }

    public override void OnRender(RenderTarget target)
    {
        if (app.LastInputMode == InputMode.Mouse)
            return;

        RefreshInputMode();
        if (_display.Length == 0)
            return;

        target.RenderRect(Vector2.Zero, Size, BackgroundColor);
        for (int i = 0; i < _display.Length; i++)
            if (!_display[i].Control.IsEmpty)
                _controlRenderer.Draw(target,
                    new Vector2(HorizontalPadding, TopHeight + RowHeight * i + 2),
                    _display[i].Control, prefix: _display[i].Prefix);
    }

    void RefreshInputMode()
    {
        InputMode inputMode = app.LastInputMode == InputMode.Gamepad
            ? InputMode.Gamepad
            : InputMode.Keyboard;
        if (_inputMode == inputMode && _language == app.Language &&
            _glyphRevision == app.Engine.InputGlyphs.Revision)
            return;

        _inputMode = inputMode;
        RefreshText();
    }

    void RefreshText()
    {
        _display = new ShortcutDisplay[_shortcuts.Length];
        int width = 0;
        for (int i = 0; i < _shortcuts.Length; i++)
        {
            InputShortcut shortcut = _shortcuts[i];
            InputControlLabel control = shortcut.Action == InputAction.None
                ? InputControlLabel.Empty
                : InputControlDisplay.Resolve(app, _inputMode, shortcut.Action,
                    shortcut.PreferredKeyboardControl, shortcut.PreferredGamepadControl,
                    shortcut.KeyboardOverride, shortcut.GamepadOverride);
            string prefix = shortcut.Hold && !control.IsEmpty
                ? InputControlDisplay.Localized(app, InputControlDisplay.Hold) + " "
                : string.Empty;
            int displayWidth = control.IsEmpty ? 0 : _controlRenderer.Measure(control, prefix: prefix);
            _display[i] = new ShortcutDisplay(control, prefix, displayWidth);
            width = System.Math.Max(width, displayWidth);
        }
        _language = app.Language;
        _glyphRevision = app.Engine.InputGlyphs.Revision;
        Size = new Vector2(width + HorizontalPadding * 2, TopHeight + RowHeight * _shortcuts.Length + 6);
    }
}
