using Burntime.Platform;
using System.Collections.Generic;

namespace Burntime.Framework;

public interface IKeyboardBindings
{
    InputAction GetAction(Key key);
    IReadOnlyList<Key> GetControls(InputAction action);
}

sealed class EmptyKeyboardBindings : IKeyboardBindings
{
    public InputAction GetAction(Key key) => InputAction.None;
    public IReadOnlyList<Key> GetControls(InputAction action) => [];
}
