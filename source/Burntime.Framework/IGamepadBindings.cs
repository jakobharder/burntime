using Burntime.Platform;

namespace Burntime.Framework;

public interface IGamepadBindings
{
    InputAction GetAction(GamepadControl control);
}

sealed class EmptyGamepadBindings : IGamepadBindings
{
    public InputAction GetAction(GamepadControl control) => InputAction.None;
}
