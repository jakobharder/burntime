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
        ("setup_notes", "f1", InputAction.SetupNotes),
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
        ("action", "q", InputAction.Action),
        ("options", "o", InputAction.Options),
        ("inventory", "e i", InputAction.Inventory),
        ("world_map", "v m", InputAction.WorldMap),
        ("statistics", "h", InputAction.Statistics),
        ("info", "r", InputAction.LocationInfo),
        ("next_turn", "t", InputAction.NextTurn),
        ("toggle_interaction", "c", InputAction.ToggleInteractionMode),
        ("show_entrances", "alt", InputAction.ShowEntrances),
    };

    static readonly Dictionary<string, Key> controls = CreateControls();

    readonly Dictionary<Key, InputAction> actions = new();
    readonly Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

    static Dictionary<string, Key> CreateControls()
    {
        Dictionary<string, Key> result = new(StringComparer.OrdinalIgnoreCase)
        {
            ["f1"] = new Key(SystemKey.F1),
            ["space"] = new Key(' '),
            ["backspace"] = new Key('\b'),
            ["enter"] = new Key(SystemKey.Enter),
            ["escape"] = new Key(SystemKey.Escape),
            ["tab"] = new Key(SystemKey.Tab),
            ["alt"] = new Key(SystemKey.Alt),
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
                value = definition.DefaultControls;

            values[definition.Setting] = value;

            // Multiple keys can be separated by whitespace or commas. An empty value is unbound.
            foreach (string name in value.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (controls.TryGetValue(name, out Key control))
                    actions[Normalize(control)] = definition.Action;
        }
    }

    public InputAction GetAction(Key key)
    {
        if (key.IsVirtual && key.VirtualKey == SystemKey.Tab)
            return (key.Modifier & ModifierKeys.Shift) != 0
                ? InputAction.PreviousTarget
                : InputAction.NextTarget;

        if (key.IsVirtual && (key.Modifier & ModifierKeys.Shift) != 0)
        {
            if (key.VirtualKey == SystemKey.Left)
                return InputAction.LeftArea;
            if (key.VirtualKey == SystemKey.Right)
                return InputAction.RightArea;
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
        if (action == InputAction.LeftArea)
            result.Add(new Key(SystemKey.Left, ModifierKeys.Shift));
        else if (action == InputAction.RightArea)
            result.Add(new Key(SystemKey.Right, ModifierKeys.Shift));
        else if (action == InputAction.PreviousTarget)
            result.Add(new Key(SystemKey.Tab, ModifierKeys.Shift));
        else if (action == InputAction.NextTarget)
            result.Add(new Key(SystemKey.Tab));
        return result;
    }

    static Key Normalize(Key key) => key.IsVirtual
        ? new Key(key.VirtualKey)
        : new Key(char.ToLowerInvariant(key.Character));
}
