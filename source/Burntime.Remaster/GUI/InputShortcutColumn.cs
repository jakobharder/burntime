using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using System;

namespace Burntime.Remaster;

/// <summary>A non-interactive shortcut column placed beside an existing menu.</summary>
public sealed class InputShortcutColumn : Window
{
    const int HorizontalPadding = 4;
    const int TopHeight = 4;
    const int RowHeight = 11;

    readonly GuiFont _font;
    string[] _gamepadControls = [];
    string[] _keyboardControls = [];
    string[] _text = [];
    InputMode _inputMode = InputMode.None;

    public PixelColor BackgroundColor { get; set; } = new(128, 0, 0, 0);

    public InputShortcutColumn(Module app)
        : base(app)
    {
        _font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray)
        {
            Borders = TextBorders.None
        };
        Layer = 200;
    }

    public void SetShortcuts(string[] gamepadControls, string[] keyboardControls)
    {
        _gamepadControls = gamepadControls;
        _keyboardControls = keyboardControls;
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
        RefreshInputMode();
        if (_text.Length == 0)
            return;

        target.RenderRect(Vector2.Zero, Size, BackgroundColor);
        for (int i = 0; i < _text.Length; i++)
            if (_text[i].Length > 0)
                _font.DrawText(target, new Vector2(HorizontalPadding, TopHeight + RowHeight * i + 2),
                    _text[i], TextAlignment.Left, VerticalTextAlignment.Top);
    }

    void RefreshInputMode()
    {
        InputMode inputMode = app.LastInputMode == InputMode.Gamepad
            ? InputMode.Gamepad
            : InputMode.Keyboard;
        if (_inputMode == inputMode)
            return;

        _inputMode = inputMode;
        RefreshText();
    }

    void RefreshText()
    {
        string[] controls = _inputMode == InputMode.Gamepad
            ? _gamepadControls
            : _keyboardControls;
        _text = new string[controls.Length];
        int width = 0;
        for (int i = 0; i < controls.Length; i++)
        {
            _text[i] = string.IsNullOrEmpty(controls[i]) ? string.Empty : $"[{controls[i]}]";
            width = System.Math.Max(width, _font.GetWidth(_text[i]));
        }
        Size = new Vector2(width + HorizontalPadding * 2, TopHeight + RowHeight * controls.Length + 6);
    }
}
