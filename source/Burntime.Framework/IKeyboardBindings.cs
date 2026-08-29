using Burntime.Platform;

namespace Burntime.Framework;

public interface IKeyboardBindings
{
    InputAction GetAction(Key key);
}

sealed class EmptyKeyboardBindings : IKeyboardBindings
{
    public InputAction GetAction(Key key) => InputAction.None;
}
