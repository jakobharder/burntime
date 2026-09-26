using Burntime.Framework;
using Burntime.Platform;
using Burntime.Platform.IO;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster;

/// <summary>Burntime game actions bound to physical gamepad buttons.</summary>
public sealed class GamepadBindings : IGamepadBindings
{
    const string SectionName = "gamepad";

    static readonly (string Setting, string DefaultControl, InputAction Action)[] definitions =
    {
        ("accept", "a", InputAction.Primary),
        ("back", "b", InputAction.Back),
        ("secondary", "x", InputAction.Secondary),
        ("action", "y", InputAction.Action),
        ("options", "menu", InputAction.Options),
        ("world_map", "view", InputAction.WorldMap),
        ("left_area", "left_shoulder", InputAction.LeftArea),
        ("right_area", "right_shoulder", InputAction.RightArea),
        ("cycle_target", "right_trigger", InputAction.NextTarget),
        ("inventory", "dpad_up", InputAction.Inventory),
        ("statistics", "dpad_left", InputAction.Statistics),
        ("info", "dpad_right", InputAction.LocationInfo),
        ("next_turn", "dpad_down", InputAction.NextTurn),
        ("show_entrances", "left_trigger", InputAction.ShowEntrances)
    };

    static readonly Dictionary<string, GamepadControl> controls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a"] = GamepadControl.A,
        ["b"] = GamepadControl.B,
        ["x"] = GamepadControl.X,
        ["y"] = GamepadControl.Y,
        ["menu"] = GamepadControl.Menu,
        ["view"] = GamepadControl.View,
        ["left_shoulder"] = GamepadControl.LeftShoulder,
        ["right_shoulder"] = GamepadControl.RightShoulder,
        ["left_stick"] = GamepadControl.LeftStick,
        ["right_stick"] = GamepadControl.RightStick,
        ["left_trigger"] = GamepadControl.LeftTrigger,
        ["right_trigger"] = GamepadControl.RightTrigger,
        ["dpad_up"] = GamepadControl.DPadUp,
        ["dpad_down"] = GamepadControl.DPadDown,
        ["dpad_left"] = GamepadControl.DPadLeft,
        ["dpad_right"] = GamepadControl.DPadRight
    };

    readonly Dictionary<GamepadControl, InputAction> actions = new();
    readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

    public void Load(ConfigFile settings)
    {
        actions.Clear();
        values.Clear();
        ConfigSection defaults = settings[SectionName];

        foreach (var definition in definitions)
        {
            string value;
            if (defaults.ContainsKey(definition.Setting))
                value = defaults.GetString(definition.Setting).Trim();
            else if (definition.Setting == "action" && defaults.ContainsKey("global_action"))
                value = defaults.GetString("global_action").Trim();
            else
                value = definition.DefaultControl;

            values[definition.Setting] = value;

            // An explicitly empty value leaves the action unbound.
            if (controls.TryGetValue(value, out GamepadControl control))
                actions[control] = definition.Action;
        }
    }

    public InputAction GetAction(GamepadControl control) =>
        actions.TryGetValue(control, out InputAction action) ? action : InputAction.None;

    public IReadOnlyList<GamepadControl> GetControls(InputAction action)
    {
        List<GamepadControl> result = [];
        foreach ((GamepadControl control, InputAction boundAction) in actions)
            if (boundAction == action)
                result.Add(control);
        return result;
    }

}
