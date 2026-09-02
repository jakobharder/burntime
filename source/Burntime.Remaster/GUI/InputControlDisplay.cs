using Burntime.Framework;
using Burntime.Platform;
using System.Collections.Generic;

namespace Burntime.Remaster;

readonly record struct InputControlPart(string Text, InputGlyph Glyph)
{
    public InputControlPart(string text) : this(text, InputGlyph.None) { }
    public InputControlPart(InputGlyph glyph) : this(string.Empty, glyph) { }
}

sealed class InputControlLabel
{
    public static InputControlLabel Empty { get; } = new([]);
    public IReadOnlyList<InputControlPart> Parts { get; }
    public bool IsEmpty => Parts.Count == 0;

    public InputControlLabel(params InputControlPart[] parts) => Parts = parts;
}

static class InputControlDisplay
{
    const int Escape = 0;
    const int Enter = 1;
    const int Tab = 2;
    const int Up = 3;
    const int Down = 4;
    const int Left = 5;
    const int Right = 6;
    const int Space = 7;
    const int Backspace = 8;
    const int Shift = 9;
    const int Alt = 10;
    public const int Hold = 11;
    const int LeftBumper = 12;
    const int RightBumper = 13;
    const int LeftStick = 14;
    const int RightStick = 15;
    const int LeftTrigger = 16;
    const int RightTrigger = 17;
    const int DPadUp = 18;
    const int DPadDown = 19;
    const int DPadLeft = 20;
    const int DPadRight = 21;
    const int Menu = 22;
    const int View = 23;
    const int DPad = 24;
    const int Stick = 25;

    public static string Localized(Module app, int index) =>
        app.ResourceManager.GetString("controls", index);

    public static InputControlLabel ResolvePair(Module app, InputMode inputMode,
        InputAction firstAction, InputAction secondAction,
        Key? preferredFirstKeyboardControl = null, Key? preferredSecondKeyboardControl = null,
        GamepadControl? preferredFirstGamepadControl = null,
        GamepadControl? preferredSecondGamepadControl = null,
        string? keyboardOverride = null, string? gamepadOverride = null)
    {
        string? controlOverride = inputMode == InputMode.Keyboard
            ? keyboardOverride
            : inputMode == InputMode.Gamepad ? gamepadOverride : null;
        if (controlOverride != null)
        {
            if (inputMode == InputMode.Gamepad && controlOverride == "D-pad Left/Right")
                return new InputControlLabel(new InputControlPart(InputGlyph.DPadHorizontal));
            return FromText(TranslateOverride(app, controlOverride));
        }

        InputControlLabel first = Resolve(app, inputMode, firstAction,
            preferredFirstKeyboardControl, preferredFirstGamepadControl);
        InputControlLabel second = Resolve(app, inputMode, secondAction,
            preferredSecondKeyboardControl, preferredSecondGamepadControl);
        if (first.IsEmpty || second.IsEmpty)
            return InputControlLabel.Empty;

        if (IsHorizontalDPadPair(first, second))
            return new InputControlLabel(new InputControlPart(InputGlyph.DPadHorizontal));

        if (TryGetText(first, out string firstText) && TryGetText(second, out string secondText))
            return FromText(Combine(firstText, secondText));

        List<InputControlPart> parts = HasGlyph(first) && HasGlyph(second)
            ? [.. first.Parts, .. second.Parts]
            : [.. first.Parts, new(" / "), .. second.Parts];
        return new(parts.ToArray());
    }

    public static InputControlLabel Resolve(Module app, InputMode inputMode, InputAction action,
        Key? preferredKeyboardControl = null, GamepadControl? preferredGamepadControl = null,
        string? keyboardOverride = null, string? gamepadOverride = null)
    {
        if (inputMode == InputMode.Keyboard)
        {
            if (keyboardOverride != null)
                return FromText(TranslateOverride(app, keyboardOverride));

            IReadOnlyList<Key> controls = app.KeyboardActionBindings.GetControls(action);
            if (controls.Count == 0)
                return InputControlLabel.Empty;
            return FromText(Format(app, FindPreferred(controls, preferredKeyboardControl)));
        }

        if (inputMode == InputMode.Gamepad)
        {
            if (gamepadOverride != null)
                return FromText(TranslateOverride(app, gamepadOverride));

            IReadOnlyList<GamepadControl> controls = app.GamepadActionBindings.GetControls(action);
            if (controls.Count == 0)
                return InputControlLabel.Empty;
            GamepadControl control = FindPreferred(controls,
                preferredGamepadControl ?? DefaultGamepadControl(action));
            IInputGlyphProvider glyphProvider = app.Engine.InputGlyphs;
            InputGlyph glyph = glyphProvider.GetGlyph(control);
            string? labelOverride = glyphProvider.GetLabelOverride(control);
            return glyph == InputGlyph.None
                ? FromText(labelOverride ?? Format(app, control, glyphProvider.LabelStyle))
                : new InputControlLabel(new InputControlPart(glyph));
        }

        return InputControlLabel.Empty;
    }

    static InputControlLabel FromText(string text) => text.Length == 0
        ? InputControlLabel.Empty
        : new(new InputControlPart(text));

    static bool TryGetText(InputControlLabel label, out string text)
    {
        if (label.Parts.Count == 1 && label.Parts[0].Glyph == InputGlyph.None)
        {
            text = label.Parts[0].Text;
            return true;
        }
        text = string.Empty;
        return false;
    }

    static bool HasGlyph(InputControlLabel label)
    {
        foreach (InputControlPart part in label.Parts)
            if (part.Glyph != InputGlyph.None)
                return true;
        return false;
    }

    static bool IsHorizontalDPadPair(InputControlLabel first, InputControlLabel second)
    {
        if (first.Parts.Count != 1 || second.Parts.Count != 1)
            return false;

        InputGlyph firstGlyph = first.Parts[0].Glyph;
        InputGlyph secondGlyph = second.Parts[0].Glyph;
        return firstGlyph == InputGlyph.DPadLeft && secondGlyph == InputGlyph.DPadRight ||
            firstGlyph == InputGlyph.DPadRight && secondGlyph == InputGlyph.DPadLeft;
    }

    static GamepadControl? DefaultGamepadControl(InputAction action) => action switch
    {
        InputAction.Statistics => GamepadControl.DPadLeft,
        InputAction.LocationInfo => GamepadControl.DPadRight,
        _ => null
    };

    static Key FindPreferred(IReadOnlyList<Key> controls, Key? preferred)
    {
        if (preferred.HasValue)
            foreach (Key control in controls)
                if (SameControl(control, preferred.Value))
                    return control;
        return controls[0];
    }

    static GamepadControl FindPreferred(IReadOnlyList<GamepadControl> controls,
        GamepadControl? preferred)
    {
        if (preferred.HasValue)
            foreach (GamepadControl control in controls)
                if (control == preferred.Value)
                    return control;
        return controls[0];
    }

    static bool SameControl(Key left, Key right) =>
        left.Character == right.Character && left.VirtualKey == right.VirtualKey &&
        left.Modifier == right.Modifier;

    static string Combine(string first, string second) => first + " / " + second;

    static string Format(Module app, Key key)
    {
        string control = key.IsVirtual
            ? key.VirtualKey switch
            {
                SystemKey.Escape => Localized(app, Escape),
                SystemKey.Enter => Localized(app, Enter),
                SystemKey.Tab => Localized(app, Tab),
                SystemKey.Up => Localized(app, Up),
                SystemKey.Down => Localized(app, Down),
                SystemKey.Left => Localized(app, Left),
                SystemKey.Right => Localized(app, Right),
                _ => key.VirtualKey.ToString()
            }
            : key.Character switch
            {
                ' ' => Localized(app, Space),
                '\b' => Localized(app, Backspace),
                _ => char.ToUpperInvariant(key.Character).ToString()
            };

        if ((key.Modifier & ModifierKeys.Shift) != 0)
            control = Localized(app, Shift) + "+" + control;
        if ((key.Modifier & ModifierKeys.LeftAlt) != 0)
            control = Localized(app, Alt) + "+" + control;
        return control;
    }

    static string Format(Module app, GamepadControl control, GamepadLabelStyle style)
    {
        if (style is GamepadLabelStyle.PlayStation or GamepadLabelStyle.Steam)
            return control switch
            {
                GamepadControl.LeftShoulder => "L1",
                GamepadControl.RightShoulder => "R1",
                GamepadControl.LeftStick => "L3",
                GamepadControl.RightStick => "R3",
                GamepadControl.LeftTrigger => "L2",
                GamepadControl.RightTrigger => "R2",
                _ => FormatDefault(app, control)
            };
        if (style == GamepadLabelStyle.Switch)
            return control switch
            {
                GamepadControl.LeftShoulder => "L",
                GamepadControl.RightShoulder => "R",
                GamepadControl.LeftStick => "LS",
                GamepadControl.RightStick => "RS",
                GamepadControl.LeftTrigger => "ZL",
                GamepadControl.RightTrigger => "ZR",
                _ => FormatDefault(app, control)
            };
        return FormatDefault(app, control);
    }

    static string FormatDefault(Module app, GamepadControl control) => control switch
    {
        GamepadControl.LeftShoulder => Localized(app, LeftBumper),
        GamepadControl.RightShoulder => Localized(app, RightBumper),
        GamepadControl.LeftStick => Localized(app, LeftStick),
        GamepadControl.RightStick => Localized(app, RightStick),
        GamepadControl.LeftTrigger => Localized(app, LeftTrigger),
        GamepadControl.RightTrigger => Localized(app, RightTrigger),
        GamepadControl.DPadUp => Localized(app, DPadUp),
        GamepadControl.DPadDown => Localized(app, DPadDown),
        GamepadControl.DPadLeft => Localized(app, DPadLeft),
        GamepadControl.DPadRight => Localized(app, DPadRight),
        GamepadControl.Menu => Localized(app, Menu),
        GamepadControl.View => Localized(app, View),
        _ => control.ToString()
    };

    static string TranslateOverride(Module app, string value) => value switch
    {
        "Shift+Up/Down" => $"{Localized(app, Shift)}+{Localized(app, Up)}/{Localized(app, Down)}",
        "D-pad Left/Right" => $"{Localized(app, DPad)} {Localized(app, Left)}/{Localized(app, Right)}",
        "D-pad/Stick Left/Right" => $"{Localized(app, DPad)}/{Localized(app, Stick)} {Localized(app, Left)}/{Localized(app, Right)}",
        _ => value
    };
}
