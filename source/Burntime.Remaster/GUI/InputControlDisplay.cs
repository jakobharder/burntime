using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using System.Collections.Generic;

namespace Burntime.Remaster;

enum KeyboardGlyph
{
    None = 0,
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    Digit0, Digit1, Digit2, Digit3, Digit4,
    Digit5, Digit6, Digit7, Digit8, Digit9,
    Escape, Enter, Tab, Space, Ctrl, Alt, Shift,
    Up, Down, Left, Right, MouseLeft, MouseRight,
    Count
}

readonly record struct InputControlPart(string Text, InputGlyph Glyph, KeyboardGlyph Keyboard)
{
    public bool HasGlyph => Glyph != InputGlyph.None || Keyboard != KeyboardGlyph.None;
    public InputControlPart(string text) : this(text, InputGlyph.None, KeyboardGlyph.None) { }
    public InputControlPart(InputGlyph glyph) : this(string.Empty, glyph, KeyboardGlyph.None) { }
    public InputControlPart(KeyboardGlyph glyph) : this(string.Empty, InputGlyph.None, glyph) { }
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
    public static string Localized(Module app, int index) =>
        app.ResourceManager.GetString("controls", index);

    public static InputControlLabel ResolvePattern(Module app, InputMode inputMode,
        InputPattern pattern)
    {
        if (inputMode == InputMode.Mouse)
            inputMode = InputMode.Keyboard;

        if (inputMode == InputMode.Keyboard)
            return pattern switch
            {
                InputPattern.HorizontalNavigation => KeyboardPair(
                    KeyboardGlyph.Left, KeyboardGlyph.Right),
                InputPattern.HorizontalPaging => KeyboardPair(
                    KeyboardGlyph.Left, KeyboardGlyph.Right, KeyboardGlyph.Shift),
                InputPattern.VerticalPaging => KeyboardPair(
                    KeyboardGlyph.Up, KeyboardGlyph.Down, KeyboardGlyph.Shift),
                _ => InputControlLabel.Empty
            };

        if (inputMode == InputMode.Gamepad)
            return pattern switch
            {
                InputPattern.HorizontalNavigation => new InputControlLabel(
                    new InputControlPart(InputGlyph.DPadHorizontal)),
                InputPattern.HorizontalPaging or InputPattern.VerticalPaging =>
                    ResolvePair(app, inputMode,
                        InputAction.LeftArea, InputAction.RightArea),
                _ => InputControlLabel.Empty
            };

        return InputControlLabel.Empty;
    }

    public static InputControlLabel Resolve(Module app, InputMode inputMode,
        InputPrompt? prompt, Key? preferredPrimaryKeyboardControl = null)
    {
        if (prompt is not InputPrompt value)
            return InputControlLabel.Empty;

        InputPattern pattern = inputMode is InputMode.Keyboard or InputMode.Mouse
            ? value.KeyboardPattern ?? value.Pattern
            : value.Pattern;
        if (pattern != InputPattern.None)
            return ResolvePattern(app, inputMode, pattern);

        Key? keyboardControl = value.KeyboardControl ??
            (value.Action == InputAction.Primary
                ? preferredPrimaryKeyboardControl
                : null);
        return Resolve(app, inputMode, value.Action, keyboardControl,
            value.GamepadControl, value.EffectiveMouseControl);
    }

    static InputControlLabel KeyboardPair(KeyboardGlyph first, KeyboardGlyph second,
        KeyboardGlyph modifier = KeyboardGlyph.None)
    {
        List<InputControlPart> parts = [];
        if (modifier != KeyboardGlyph.None)
            parts.Add(new InputControlPart(modifier));
        parts.Add(new InputControlPart(first));
        parts.Add(new InputControlPart(second));
        return new InputControlLabel(parts.ToArray());
    }

    static InputControlLabel ResolvePair(Module app, InputMode inputMode,
        InputAction firstAction, InputAction secondAction,
        Key? firstKeyboardControl = null,
        GamepadControl? firstGamepadControl = null,
        MouseButton? firstMouseControl = null)
    {
        InputControlLabel first = Resolve(app, inputMode, firstAction,
            firstKeyboardControl, firstGamepadControl,
            mouseControl: firstMouseControl);
        InputControlLabel second = Resolve(app, inputMode, secondAction);
        if (first.IsEmpty || second.IsEmpty)
            return InputControlLabel.Empty;

        if (IsHorizontalDPadPair(first, second))
            return new InputControlLabel(new InputControlPart(InputGlyph.DPadHorizontal));

        if (TryGetText(first, out string firstText) && TryGetText(second, out string secondText))
            return FromText(Combine(firstText, secondText));

        List<InputControlPart> parts = HasGlyph(first) && HasGlyph(second)
            ? CombineGlyphs(first, second)
            : [.. first.Parts, new(" / "), .. second.Parts];
        return new(parts.ToArray());
    }

    static List<InputControlPart> CombineGlyphs(InputControlLabel first,
        InputControlLabel second)
    {
        int sharedModifiers = 0;
        while (sharedModifiers < first.Parts.Count &&
            sharedModifiers < second.Parts.Count &&
            IsKeyboardModifier(first.Parts[sharedModifiers]) &&
            first.Parts[sharedModifiers].Keyboard == second.Parts[sharedModifiers].Keyboard)
        {
            sharedModifiers++;
        }

        List<InputControlPart> parts = [.. first.Parts];
        for (int i = sharedModifiers; i < second.Parts.Count; i++)
            parts.Add(second.Parts[i]);
        return parts;
    }

    static bool IsKeyboardModifier(InputControlPart part) =>
        part.Keyboard is KeyboardGlyph.Ctrl or KeyboardGlyph.Alt or KeyboardGlyph.Shift;

    public static InputControlLabel Resolve(Module app, InputMode inputMode, InputAction action,
        Key? preferredKeyboardControl = null, GamepadControl? preferredGamepadControl = null,
        MouseButton? mouseControl = null)
    {
        if (inputMode == InputMode.Mouse)
        {
            MouseButton? control = mouseControl ??
                InputPrompt.DefaultMouseControl(action);
            if (control.HasValue)
            {
                KeyboardGlyph glyph = control.Value switch
                {
                    MouseButton.Left => KeyboardGlyph.MouseLeft,
                    MouseButton.Right => KeyboardGlyph.MouseRight,
                    _ => KeyboardGlyph.None
                };
                return glyph == KeyboardGlyph.None
                    ? InputControlLabel.Empty
                    : new InputControlLabel(new InputControlPart(glyph));
            }

            // Mouse users can still use the regular keyboard shortcuts.
            inputMode = InputMode.Keyboard;
        }

        if (inputMode == InputMode.Keyboard)
        {
            IReadOnlyList<Key> controls = app.KeyboardActionBindings.GetControls(action);
            if (controls.Count == 0)
                return InputControlLabel.Empty;
            return FormatKeyboard(app, FindPreferred(controls, preferredKeyboardControl));
        }

        if (inputMode == InputMode.Gamepad)
        {
            IReadOnlyList<GamepadControl> controls = app.GamepadActionBindings.GetControls(action);
            if (controls.Count == 0 && !preferredGamepadControl.HasValue)
                return InputControlLabel.Empty;
            GamepadControl control = controls.Count == 0
                ? preferredGamepadControl!.Value
                : FindPreferred(controls,
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
        if (label.Parts.Count == 1 && !label.Parts[0].HasGlyph)
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
            if (part.HasGlyph)
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

    static InputControlLabel FormatKeyboard(Module app, Key key)
    {
        KeyboardGlyph glyph = GetKeyboardGlyph(key);
        if (glyph == KeyboardGlyph.None)
            return FromText(Format(app, key));

        List<InputControlPart> parts = [];
        if ((key.Modifier & ModifierKeys.Shift) != 0)
            parts.Add(new InputControlPart(KeyboardGlyph.Shift));
        if ((key.Modifier & ModifierKeys.LeftAlt) != 0)
            parts.Add(new InputControlPart(KeyboardGlyph.Alt));
        parts.Add(new InputControlPart(glyph));
        return new InputControlLabel(parts.ToArray());
    }

    static KeyboardGlyph GetKeyboardGlyph(Key key)
    {
        if (!key.IsVirtual)
        {
            char character = char.ToLowerInvariant(key.Character);
            if (character is >= 'a' and <= 'z')
                return (KeyboardGlyph)((int)KeyboardGlyph.A + character - 'a');
            if (character is >= '0' and <= '9')
                return (KeyboardGlyph)((int)KeyboardGlyph.Digit0 + character - '0');
            return character switch
            {
                ' ' => KeyboardGlyph.Space,
                _ => KeyboardGlyph.None
            };
        }

        return key.VirtualKey switch
        {
            SystemKey.Escape => KeyboardGlyph.Escape,
            SystemKey.Enter => KeyboardGlyph.Enter,
            SystemKey.Tab => KeyboardGlyph.Tab,
            SystemKey.Alt => KeyboardGlyph.Alt,
            SystemKey.Ctrl => KeyboardGlyph.Ctrl,
            SystemKey.Up => KeyboardGlyph.Up,
            SystemKey.Down => KeyboardGlyph.Down,
            SystemKey.Left => KeyboardGlyph.Left,
            SystemKey.Right => KeyboardGlyph.Right,
            _ => KeyboardGlyph.None
        };
    }

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
}
