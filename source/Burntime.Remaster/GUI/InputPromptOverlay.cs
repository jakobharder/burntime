using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster;

public readonly record struct InputPrompt(InputAction Action, GuiString Label)
{
    public InputAction AlternateAction { get; init; }
    public Key? PreferredKeyboardControl { get; init; }
    public Key? PreferredAlternateKeyboardControl { get; init; }
    public GamepadControl? PreferredGamepadControl { get; init; }
    public GamepadControl? PreferredAlternateGamepadControl { get; init; }
    public string? KeyboardOverride { get; init; }
    public string? GamepadOverride { get; init; }
}

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
    readonly List<InputPrompt> _prompts = [];
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

    public void SetPrompts(params InputPrompt[] prompts)
    {
        if (PromptsEqual(_prompts, prompts))
            return;

        _prompts.Clear();
        _prompts.AddRange(prompts);
        RefreshText();
    }

    static bool PromptsEqual(List<InputPrompt> current, InputPrompt[] prompts)
    {
        if (current.Count != prompts.Length)
            return false;

        for (int i = 0; i < prompts.Length; i++)
            if (current[i].Action != prompts[i].Action ||
                current[i].AlternateAction != prompts[i].AlternateAction ||
                current[i].Label.ID != prompts[i].Label.ID ||
                !Nullable.Equals(current[i].PreferredKeyboardControl,
                    prompts[i].PreferredKeyboardControl) ||
                !Nullable.Equals(current[i].PreferredAlternateKeyboardControl,
                    prompts[i].PreferredAlternateKeyboardControl) ||
                current[i].PreferredGamepadControl != prompts[i].PreferredGamepadControl ||
                current[i].PreferredAlternateGamepadControl !=
                    prompts[i].PreferredAlternateGamepadControl ||
                current[i].KeyboardOverride != prompts[i].KeyboardOverride ||
                current[i].GamepadOverride != prompts[i].GamepadOverride)
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
        List<string> text = [];
        List<int> textWidths = [];
        int width = 0;
        foreach (InputPrompt prompt in _prompts)
        {
            string control = prompt.AlternateAction == InputAction.None
                ? InputControlDisplay.Resolve(app, _inputMode, prompt.Action,
                    prompt.PreferredKeyboardControl, prompt.PreferredGamepadControl,
                    prompt.KeyboardOverride, prompt.GamepadOverride)
                : InputControlDisplay.ResolvePair(app, _inputMode,
                    prompt.Action, prompt.AlternateAction,
                    prompt.PreferredKeyboardControl, prompt.PreferredAlternateKeyboardControl,
                    prompt.PreferredGamepadControl, prompt.PreferredAlternateGamepadControl,
                    prompt.KeyboardOverride, prompt.GamepadOverride);
            if (control.Length == 0)
                continue;

            string label = prompt.Label;
            string value = label.Length == 0 ? $"[{control}]" : $"[{control}] {label}";
            text.Add(value);
            int textWidth = _font.GetWidth(value);
            textWidths.Add(textWidth);
            width += textWidth;
        }

        _text = text.ToArray();
        _textWidths = textWidths.ToArray();

        if (_text.Length > 1)
            width += _font.GetWidth(Separator) * (_text.Length - 1);
        _language = app.Language;
        Size = new Vector2(
            width + HorizontalPadding * 2,
            _font.GetHeight() + VerticalPadding * 2);
    }
}
