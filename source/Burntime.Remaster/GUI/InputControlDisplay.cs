using Burntime.Framework;
using Burntime.Platform;
using System.Collections.Generic;

namespace Burntime.Remaster;

static class InputControlDisplay
{
    public static string ResolvePair(Module app, InputMode inputMode,
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
            return controlOverride;

        string first = Resolve(app, inputMode, firstAction,
            preferredFirstKeyboardControl, preferredFirstGamepadControl);
        string second = Resolve(app, inputMode, secondAction,
            preferredSecondKeyboardControl, preferredSecondGamepadControl);
        if (first.Length == 0 || second.Length == 0)
            return string.Empty;
        return Combine(first, second);
    }

    public static string Resolve(Module app, InputMode inputMode, InputAction action,
        Key? preferredKeyboardControl = null, GamepadControl? preferredGamepadControl = null,
        string? keyboardOverride = null, string? gamepadOverride = null)
    {
        if (inputMode == InputMode.Keyboard)
        {
            if (keyboardOverride != null)
                return keyboardOverride;

            IReadOnlyList<Key> controls = app.KeyboardActionBindings.GetControls(action);
            if (controls.Count == 0)
                return string.Empty;
            Key control = FindPreferred(controls, preferredKeyboardControl);
            return Format(control);
        }

        if (inputMode == InputMode.Gamepad)
        {
            if (gamepadOverride != null)
                return gamepadOverride;

            IReadOnlyList<GamepadControl> controls = app.GamepadActionBindings.GetControls(action);
            if (controls.Count == 0)
                return string.Empty;
            GamepadControl control = FindPreferred(controls,
                preferredGamepadControl ?? DefaultGamepadControl(action));
            return Format(control);
        }

        return string.Empty;
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

    static string Combine(string first, string second)
    {
        int commonLength = 0;
        int maximum = System.Math.Min(first.Length, second.Length);
        while (commonLength < maximum && first[commonLength] == second[commonLength])
            commonLength++;

        while (commonLength > 0 && first[commonLength - 1] is not ('+' or ' '))
            commonLength--;
        return commonLength == 0
            ? first + "/" + second
            : first + "/" + second[commonLength..];
    }

    static string Format(Key key)
    {
        string control = key.IsVirtual
            ? key.VirtualKey switch
            {
                SystemKey.Escape => "Esc",
                SystemKey.Enter => "Enter",
                SystemKey.Tab => "Tab",
                SystemKey.Up => "Up",
                SystemKey.Down => "Down",
                SystemKey.Left => "Left",
                SystemKey.Right => "Right",
                _ => key.VirtualKey.ToString()
            }
            : key.Character switch
            {
                ' ' => "Space",
                '\b' => "Backspace",
                _ => char.ToUpperInvariant(key.Character).ToString()
            };

        if ((key.Modifier & ModifierKeys.Shift) != 0)
            control = "Shift+" + control;
        if ((key.Modifier & ModifierKeys.LeftAlt) != 0)
            control = "Alt+" + control;
        return control;
    }

    static string Format(GamepadControl control) => control switch
    {
        GamepadControl.LeftShoulder => "LB",
        GamepadControl.RightShoulder => "RB",
        GamepadControl.LeftStick => "Left stick",
        GamepadControl.RightStick => "Right stick",
        GamepadControl.LeftTrigger => "LT",
        GamepadControl.RightTrigger => "RT",
        GamepadControl.DPadUp => "D-pad Up",
        GamepadControl.DPadDown => "D-pad Down",
        GamepadControl.DPadLeft => "D-pad Left",
        GamepadControl.DPadRight => "D-pad Right",
        _ => control.ToString()
    };
}
