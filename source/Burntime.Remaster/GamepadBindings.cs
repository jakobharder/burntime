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
        ("global_action", "y", InputAction.GlobalAction),
        ("options", "menu", InputAction.Options),
        ("world_map", "view", InputAction.WorldMap),
        ("left_area", "left_shoulder", InputAction.Statistics),
        ("right_area", "right_shoulder", InputAction.LocationInfo),
        ("inventory", "dpad_up", InputAction.Inventory),
        ("statistics", "dpad_left", InputAction.Statistics),
        ("info", "dpad_right", InputAction.LocationInfo),
        ("next_turn", "dpad_down", InputAction.NextTurn)
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

    public void Load(ConfigFile settings, ConfigFile userSettings)
    {
        actions.Clear();
        values.Clear();
        ConfigSection defaults = settings[SectionName];
        ConfigSection overrides = userSettings[SectionName];

        foreach (var definition in definitions)
        {
            string value = defaults.ContainsKey(definition.Setting)
                ? defaults.GetString(definition.Setting).Trim()
                : definition.DefaultControl;

            if (overrides.ContainsKey(definition.Setting))
                value = overrides.GetString(definition.Setting).Trim();

            values[definition.Setting] = value;

            // An explicitly empty value leaves the action unbound.
            if (controls.TryGetValue(value, out GamepadControl control))
                actions[control] = definition.Action;
        }
    }

    public InputAction GetAction(GamepadControl control) =>
        actions.TryGetValue(control, out InputAction action) ? action : InputAction.None;

    public void Save(ConfigFile config)
    {
        ConfigSection section = config.GetSection(SectionName, true);
        foreach (var definition in definitions)
            section.Set(definition.Setting, values.TryGetValue(definition.Setting, out string value)
                ? value
                : definition.DefaultControl);
    }
}
