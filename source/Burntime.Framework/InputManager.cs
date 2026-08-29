using System.Collections.Generic;
using System.Linq;

namespace Burntime.Framework;

public enum InputSource
{
    Keyboard,
    GamepadOne
}

/// <summary>
/// Stores semantic actions after physical input has been translated by the game.
/// </summary>
public sealed class InputManager
{
    readonly List<InputAction> actions = new();
    readonly Dictionary<InputSource, HashSet<InputAction>> actionsDown = new();

    public InputAction[] Actions
    {
        get
        {
            lock (actions)
                return actions.ToArray();
        }
    }

    public InputAction[] ConsumeActions()
    {
        lock (actions)
        {
            InputAction[] result = actions.ToArray();
            actions.Clear();
            return result;
        }
    }

    public InputAction[] ActionsDown
    {
        get
        {
            lock (actionsDown)
                return actionsDown.Values.SelectMany(value => value).Distinct().ToArray();
        }
    }

    public void Press(InputAction action)
    {
        if (action == InputAction.None)
            return;
        lock (actions)
            actions.Add(action);
    }

    public void SetDown(InputSource source, InputAction action, bool isDown)
    {
        lock (actionsDown)
        {
            if (!actionsDown.TryGetValue(source, out HashSet<InputAction> sourceActions))
            {
                sourceActions = new HashSet<InputAction>();
                actionsDown.Add(source, sourceActions);
            }
            if (isDown)
                sourceActions.Add(action);
            else
                sourceActions.Remove(action);
        }
    }

    public void ClearDown(InputSource source)
    {
        lock (actionsDown)
            if (actionsDown.TryGetValue(source, out HashSet<InputAction> sourceActions))
                sourceActions.Clear();
    }

}
