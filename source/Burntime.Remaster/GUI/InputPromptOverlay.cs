using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster;

public readonly record struct InputPrompt(string Control, GuiString Label);

/// <summary>
/// A scene-owned, non-interactive row of contextual input prompts.
/// Scenes place the overlay and replace its prompts when their focus changes.
/// </summary>
public sealed class InputPromptOverlay : Window
{
    const int HorizontalPadding = 4;
    const int VerticalPadding = 2;
    const string Separator = "   ";

    readonly GuiFont _font;
    readonly List<InputPrompt> _gamepadPrompts = [];
    readonly List<InputPrompt> _keyboardPrompts = [];
    string[] _text = [];
    int[] _textWidths = [];
    string _language = string.Empty;
    InputMode _inputMode = InputMode.None;

    public PixelColor BackgroundColor { get; set; } = new(128, 0, 0, 0);

    public InputPromptOverlay(Module app)
        : base(app)
    {
        _font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray)
        {
            Borders = TextBorders.None
        };
        HorizontalAlignment = PositionAlignment.Right;
        VerticalAlignment = PositionAlignment.Right;
        // MonoGame maps layers 0..255 into clip-space depth. Keep this well
        // above normal scene UI (currently <= 60), but inside that range.
        Layer = 200;
        RefreshText();
    }

    public void AnchorToScreenBottomRight(int margin = 6)
    {
        Vector2 parentPosition = Parent?.PositionOnScreen ?? Vector2.Zero;
        Position = app.Engine.Resolution.Game - parentPosition - margin;
    }

    public void SetGamepadPrompts(params InputPrompt[] prompts) =>
        SetPrompts(_gamepadPrompts, InputMode.Gamepad, prompts);

    public void SetKeyboardPrompts(params InputPrompt[] prompts) =>
        SetPrompts(_keyboardPrompts, InputMode.Keyboard, prompts);

    void SetPrompts(List<InputPrompt> target, InputMode inputMode, InputPrompt[] prompts)
    {
        if (PromptsEqual(target, prompts))
            return;

        target.Clear();
        target.AddRange(prompts);
        if (_inputMode == inputMode)
            RefreshText();
    }

    static bool PromptsEqual(List<InputPrompt> current, InputPrompt[] prompts)
    {
        if (current.Count != prompts.Length)
            return false;

        for (int i = 0; i < prompts.Length; i++)
            if (current[i].Control != prompts[i].Control ||
                current[i].Label.ID != prompts[i].Label.ID)
                return false;

        return true;
    }

    public override void OnRender(RenderTarget target)
    {
        if (app.LastInputMode is not (InputMode.Keyboard or InputMode.Gamepad))
            return;

        if (_inputMode != app.LastInputMode || _language != app.Language)
        {
            _inputMode = app.LastInputMode;
            RefreshText();
        }
        if (_text.Length == 0)
            return;

        target.RenderRect(Vector2.Zero, Size, BackgroundColor);

        // Lay out from right to left. This keeps trailing/global prompts at the
        // exact same pixel when contextual prompts are inserted before them.
        int x = Size.x - HorizontalPadding;
        int separatorWidth = _font.GetWidth(Separator);
        for (int i = _text.Length - 1; i >= 0; i--)
        {
            _font.DrawText(target, new Vector2(x, VerticalPadding), _text[i],
                TextAlignment.Right, VerticalTextAlignment.Top);
            x -= _textWidths[i] + separatorWidth;
        }
    }

    void RefreshText()
    {
        List<InputPrompt> prompts = _inputMode == InputMode.Keyboard
            ? _keyboardPrompts
            : _gamepadPrompts;
        _text = new string[prompts.Count];
        _textWidths = new int[prompts.Count];
        int width = 0;
        for (int i = 0; i < prompts.Count; i++)
        {
            string label = prompts[i].Label;
            _text[i] = label.Length == 0
                ? $"[{prompts[i].Control}]"
                : $"[{prompts[i].Control}] {label}";
            _textWidths[i] = _font.GetWidth(_text[i]);
            width += _textWidths[i];
        }

        if (_text.Length > 1)
            width += _font.GetWidth(Separator) * (_text.Length - 1);
        _language = app.Language;
        Size = new Vector2(
            width + HorizontalPadding * 2,
            _font.GetHeight() + VerticalPadding * 2);
    }
}
