using Burntime.Framework;
using Burntime.Platform;

namespace Burntime.Remaster;

public readonly record struct InputShortcut(InputAction Action)
{
    public Key? PreferredKeyboardControl { get; init; }
    public Key? PreferredMouseKeyboardControl { get; init; }
    public GamepadControl? PreferredGamepadControl { get; init; }
    public bool Hold { get; init; }
}
