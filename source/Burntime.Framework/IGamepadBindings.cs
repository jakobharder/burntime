using Burntime.Platform;
using System.Collections.Generic;

namespace Burntime.Framework;

public interface IGamepadBindings
{
    InputAction GetAction(GamepadControl control);
    IReadOnlyList<GamepadControl> GetControls(InputAction action);
}

sealed class EmptyGamepadBindings : IGamepadBindings
{
    public InputAction GetAction(GamepadControl control) => InputAction.None;
    public IReadOnlyList<GamepadControl> GetControls(InputAction action) => [];
}
