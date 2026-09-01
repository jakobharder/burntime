using Burntime.Framework;
using Burntime.Platform;
using Burntime.Platform.IO;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster;

/// <summary>Burntime game actions bound to keyboard keys.</summary>
public sealed class KeyboardBindings : IKeyboardBindings
{
    const string SectionName = "keyboard";

    static readonly (string Setting, string DefaultControls, InputAction Action)[] definitions =
    {
        ("move_up", "up", InputAction.MoveUp),
        ("move_down", "down", InputAction.MoveDown),
        ("move_left", "left", InputAction.MoveLeft),
        ("move_right", "right", InputAction.MoveRight),
        ("pan_up", "w", InputAction.PanCameraUp),
        ("pan_down", "s", InputAction.PanCameraDown),
        ("pan_left", "a", InputAction.PanCameraLeft),
        ("pan_right", "d", InputAction.PanCameraRight),
        ("accept", "space enter", InputAction.Primary),
        ("back", "escape", InputAction.Back),
        ("secondary", "f", InputAction.Secondary),
        ("action", "x", InputAction.SceneAction),
        ("options", "o", InputAction.Options),
        ("inventory", "r i", InputAction.Inventory),
        ("world_map", "v m", InputAction.WorldMap),
        ("statistics", "q", InputAction.Statistics),
        ("info", "e", InputAction.LocationInfo),
        ("next_turn", "tab", InputAction.NextTurn),
        ("toggle_interaction", "c", InputAction.ToggleInteractionMode),
    };

    static readonly Dictionary<string, Key> controls = CreateControls();

    readonly Dictionary<Key, InputAction> actions = new();
    readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

    static Dictionary<string, Key> CreateControls()
    {
        Dictionary<string, Key> result = new(StringComparer.OrdinalIgnoreCase)
        {
            ["space"] = new Key(' '),
            ["backspace"] = new Key('\b'),
            ["enter"] = new Key(SystemKey.Enter),
            ["escape"] = new Key(SystemKey.Escape),
            ["tab"] = new Key(SystemKey.Tab),
            ["up"] = new Key(SystemKey.Up),
            ["down"] = new Key(SystemKey.Down),
            ["left"] = new Key(SystemKey.Left),
            ["right"] = new Key(SystemKey.Right)
        };

        for (char key = 'a'; key <= 'z'; ++key)
            result[key.ToString()] = new Key(key);
        for (char key = '0'; key <= '9'; ++key)
            result[key.ToString()] = new Key(key);

        return result;
    }

    public void Load(ConfigFile settings, ConfigFile userSettings)
    {
        actions.Clear();
        values.Clear();
        ConfigSection defaults = settings[SectionName];
        ConfigSection overrides = userSettings[SectionName];

        foreach (var definition in definitions)
        {
            string value;
            if (defaults.ContainsKey(definition.Setting))
                value = defaults.GetString(definition.Setting).Trim();
            else if (definition.Setting == "action" && defaults.ContainsKey("global_action"))
                value = defaults.GetString("global_action").Trim();
            else
                value = definition.DefaultControls;

            if (overrides.ContainsKey(definition.Setting))
                value = overrides.GetString(definition.Setting).Trim();
            else if (definition.Setting == "action" && overrides.ContainsKey("global_action"))
                value = overrides.GetString("global_action").Trim();

            values[definition.Setting] = value;

            // Multiple keys can be separated by whitespace or commas. An empty value is unbound.
            foreach (string name in value.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (controls.TryGetValue(name, out Key control))
                    actions[Normalize(control)] = definition.Action;
        }
    }

    public InputAction GetAction(Key key)
    {
        if (key.IsVirtual && (key.Modifier & ModifierKeys.Shift) != 0)
        {
            if (key.VirtualKey == SystemKey.Left)
                return InputAction.Statistics;
            if (key.VirtualKey == SystemKey.Right)
                return InputAction.LocationInfo;
        }

        return actions.TryGetValue(Normalize(key), out InputAction action) ? action : InputAction.None;
    }

    public IReadOnlyList<Key> GetControls(InputAction action)
    {
        List<Key> result = [];
        foreach (var definition in definitions)
        {
            if (definition.Action != action ||
                !values.TryGetValue(definition.Setting, out string? value))
                continue;

            foreach (string name in value.Split(new[] { ' ', '\t', ',' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                if (controls.TryGetValue(name, out Key control) &&
                    actions.TryGetValue(Normalize(control), out InputAction boundAction) &&
                    boundAction == action)
                    result.Add(control);
            }
        }

        // These navigation chords are intentionally always available in addition
        // to the configurable bindings.
        if (action is InputAction.Statistics or InputAction.LeftArea)
            result.Add(new Key(SystemKey.Left, ModifierKeys.Shift));
        else if (action is InputAction.LocationInfo or InputAction.RightArea)
            result.Add(new Key(SystemKey.Right, ModifierKeys.Shift));
        return result;
    }

    public void Save(ConfigFile config)
    {
        ConfigSection section = config.GetSection(SectionName, true);
        foreach (var definition in definitions)
            section.Set(definition.Setting, values.TryGetValue(definition.Setting, out string value)
                ? value
                : definition.DefaultControls);
    }

    static Key Normalize(Key key) => key.IsVirtual
        ? new Key(key.VirtualKey)
        : new Key(char.ToLowerInvariant(key.Character));
}
