using Burntime.Platform;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Burntime.Framework.GUI;

public enum InputPattern
{
    None = 0,
    HorizontalNavigation,
    HorizontalPaging,
    VerticalPaging
}

public enum InputPromptPosition
{
    Primary,
    Secondary,
    Extended
}

public readonly record struct InputPrompt
{
    public InputAction Action { get; }
    public InputPattern Pattern { get; }
    public GuiString Label { get; }
    public InputPattern? KeyboardPattern { get; init; }
    public Key? KeyboardControl { get; init; }
    public GamepadControl? GamepadControl { get; init; }
    public MouseButton? MouseControl { get; init; }
    public bool ShowInMouseMode { get; init; }

    public InputPrompt(InputAction action, GuiString label)
    {
        Action = action;
        Label = label;
    }

    public InputPrompt(InputPattern pattern, GuiString label)
    {
        Pattern = pattern;
        Label = label;
    }

    public bool IsEmpty => Action == InputAction.None && Pattern == InputPattern.None;

    public bool Equals(InputPrompt other) =>
        Action == other.Action &&
        Pattern == other.Pattern &&
        Label?.ID == other.Label?.ID &&
        KeyboardPattern == other.KeyboardPattern &&
        Nullable.Equals(KeyboardControl, other.KeyboardControl) &&
        GamepadControl == other.GamepadControl &&
        MouseControl == other.MouseControl &&
        ShowInMouseMode == other.ShowInMouseMode;

    public override int GetHashCode() => HashCode.Combine(Action, Pattern,
        Label?.ID, KeyboardPattern, KeyboardControl, GamepadControl, MouseControl, ShowInMouseMode);

    public MouseButton? EffectiveMouseControl => MouseControl ??
        DefaultMouseControl(Action);

    public static MouseButton? DefaultMouseControl(InputAction action) => action switch
    {
        InputAction.Primary => MouseButton.Left,
        InputAction.Secondary or InputAction.Action => MouseButton.Right,
        _ => null
    };
}

sealed class InputPromptEntry
{
    readonly Func<bool>? _condition;
    readonly Func<InputPrompt?>? _selector;

    internal InputPromptEntry(InputPrompt prompt, Func<bool>? condition,
        Vector2? position = null,
        PositionAlignment horizontalAlignment = PositionAlignment.Left,
        PositionAlignment verticalAlignment = PositionAlignment.Left,
        bool showBackground = true, int horizontalPadding = 4,
        string separator = "   ")
    {
        Candidates = [prompt];
        _condition = condition;
        Position = position;
        HorizontalAlignment = horizontalAlignment;
        VerticalAlignment = verticalAlignment;
        ShowBackground = showBackground;
        HorizontalPadding = horizontalPadding;
        Separator = separator;
    }

    internal InputPromptEntry(Func<InputPrompt?> selector,
        IReadOnlyList<InputPrompt> candidates,
        InputPromptPosition? contextPosition = null)
    {
        _selector = selector;
        Candidates = candidates;
        ContextPosition = contextPosition;
        ShowBackground = true;
        HorizontalPadding = 4;
        Separator = "   ";
    }

    public IReadOnlyList<InputPrompt> Candidates { get; }
    public InputPromptPosition? ContextPosition { get; }
    public Vector2? Position { get; internal set; }
    public PositionAlignment HorizontalAlignment { get; }
    public PositionAlignment VerticalAlignment { get; }
    public bool ShowBackground { get; }
    public int HorizontalPadding { get; }
    public string Separator { get; }
    public bool IsInline => Position.HasValue;
    public InputPrompt? Resolve()
    {
        if (_selector != null)
            return _selector();
        return (_condition?.Invoke() ?? true) ? Candidates[0] : null;
    }
}

public sealed class InputPromptCollection : IEnumerable
{
    readonly List<InputPromptEntry> _entries = [];
    readonly List<Func<bool>> _suppressors = [];

    internal IReadOnlyList<InputPromptEntry> Entries => _entries;
    internal Window Owner { get; }
    public bool HideInNewGfx { get; set; }
    internal bool IsSuppressed
    {
        get
        {
            foreach (Func<bool> suppressor in _suppressors)
                if (suppressor())
                    return true;
            return false;
        }
    }

    internal InputPromptCollection(Window owner) => Owner = owner;

    public void Add(InputAction action, GuiString label,
        Func<bool>? condition = null) =>
        Add(new InputPrompt(action, label), condition);

    public void Add(InputPattern pattern, GuiString label,
        Func<bool>? condition = null) =>
        Add(new InputPrompt(pattern, label), condition);

    public void Add(InputPrompt prompt, Func<bool>? condition = null) =>
        _entries.Add(new InputPromptEntry(prompt, condition));

    public void AddDynamic(Func<InputPrompt?> selector,
        params InputPrompt[] candidates)
        => AddDynamicCore(null, selector, candidates);

    public void AddDynamic(InputPromptPosition position,
        Func<InputPrompt?> selector, params InputPrompt[] candidates)
        => AddDynamicCore(position, selector, candidates);

    void AddDynamicCore(InputPromptPosition? position, Func<InputPrompt?> selector,
        InputPrompt[] candidates)
    {
        if (candidates.Length == 0)
            throw new ArgumentException("At least one prompt candidate is required.",
                nameof(candidates));
        _entries.Add(new InputPromptEntry(selector, candidates, position));
    }

    public void AddDynamic(InputAction action, Func<GuiString?> labelSelector,
        params GuiString[] candidateLabels)
    {
        InputPrompt? SelectPrompt()
        {
            GuiString? label = labelSelector();
            return label == null ? null : new InputPrompt(action, label);
        }

        InputPrompt[] candidates = new InputPrompt[candidateLabels.Length];
        for (int i = 0; i < candidateLabels.Length; i++)
            candidates[i] = new InputPrompt(action, candidateLabels[i]);
        AddDynamic(SelectPrompt, candidates);
    }

    public InputPromptHandle Add(InputAction action, GuiString label,
        Vector2 position, Func<bool>? condition = null,
        PositionAlignment horizontalAlignment = PositionAlignment.Left,
        PositionAlignment verticalAlignment = PositionAlignment.Left,
        bool showBackground = true, int horizontalPadding = 4,
        string separator = "   ") =>
        Add(new InputPrompt(action, label), position, condition,
            horizontalAlignment, verticalAlignment, showBackground, horizontalPadding,
            separator);

    public InputPromptHandle Add(InputPrompt prompt, Vector2 position,
        Func<bool>? condition = null,
        PositionAlignment horizontalAlignment = PositionAlignment.Left,
        PositionAlignment verticalAlignment = PositionAlignment.Left,
        bool showBackground = true, int horizontalPadding = 4,
        string separator = "   ")
    {
        InputPromptEntry entry = new(prompt, condition, position,
            horizontalAlignment, verticalAlignment, showBackground, horizontalPadding,
            separator);
        _entries.Add(entry);
        return new InputPromptHandle(entry);
    }

    public void SuppressWhen(Func<bool> condition) => _suppressors.Add(condition);

    IEnumerator IEnumerable.GetEnumerator() => _entries.GetEnumerator();
}

public sealed class InputPromptHandle
{
    readonly InputPromptEntry _entry;

    internal InputPromptHandle(InputPromptEntry entry) => _entry = entry;

    public void UpdatePosition(Vector2 position) => _entry.Position = position;
}

public sealed record InputPromptContextGroup(
    IReadOnlyList<InputPrompt> Candidates, InputPrompt? ActivePrompt);

public sealed record InputPromptInlineEntry(InputPrompt Prompt, Vector2 Position,
    PositionAlignment HorizontalAlignment, PositionAlignment VerticalAlignment,
    bool ShowBackground, int HorizontalPadding, string Separator);

public sealed record InputPromptLayout(IReadOnlyList<InputPrompt> ScenePrompts,
    IReadOnlyList<InputPromptContextGroup> ContextGroups,
    IReadOnlyList<InputPromptInlineEntry> InlinePrompts);

public sealed class InputPromptController
{
    readonly Module _app;
    readonly Scene _scene;
    readonly InputPromptCollection _scenePrompts;

    public Key PreferredPrimaryKeyboardControl =>
        _scene.PreferredPrimaryKeyboardControl;

    public InputPromptController(Module app, InputPromptCollection scenePrompts)
    {
        if (scenePrompts.Owner is not Scene scene)
            throw new ArgumentException("The root prompt collection must belong to a scene.",
                nameof(scenePrompts));
        _app = app;
        _scene = scene;
        _scenePrompts = scenePrompts;
    }

    bool IsSuppressed(InputPromptCollection prompts) => prompts.IsSuppressed ||
        (prompts.HideInNewGfx && _app.IsNewGfx);

    public InputPromptLayout Resolve()
    {
        bool mouseInput = _app.LastInputMode == InputMode.Mouse;
        List<InputPrompt> scenePrompts = [];
        Dictionary<InputPromptPosition, List<ContextCandidate>> candidates = [];

        if (!IsSuppressed(_scenePrompts))
        {
            foreach (InputPromptEntry entry in _scenePrompts.Entries)
            {
                if (entry.IsInline)
                    continue;

                InputPrompt? activePrompt = entry.Resolve();
                if (!mouseInput || activePrompt?.ShowInMouseMode == true)
                {
                    if (activePrompt.HasValue)
                        scenePrompts.Add(activePrompt.Value);
                    continue;
                }

                AddEntryContextCandidates(entry, activePrompt, candidates,
                    ownerIsActive: true, mouseInput, isProjectedScene: true);
            }
        }

        Window? inputRoot = _app.SceneManager.InputWindow;
        if (inputRoot != null)
            AddWindowContextCandidates(inputRoot, candidates, mouseInput,
                inputRoot != _scene);

        List<InputPromptContextGroup> contextGroups = [];
        bool hasExtended = candidates.ContainsKey(InputPromptPosition.Extended);
        // Navigation extends the secondary side of the row, while ordinary
        // context rows read primary then secondary from left to right.
        InputPromptPosition[] roleOrder = hasExtended
            ? [InputPromptPosition.Extended, InputPromptPosition.Secondary,
                InputPromptPosition.Primary]
            : [InputPromptPosition.Primary, InputPromptPosition.Secondary];
        foreach (InputPromptPosition role in roleOrder)
        {
            if (!candidates.TryGetValue(role,
                out List<ContextCandidate>? slotCandidates))
            {
                continue;
            }

            ContextCandidate? active = null;
            List<InputPrompt> prompts = [];
            foreach (ContextCandidate candidate in slotCandidates)
            {
                prompts.Add(candidate.Prompt);
                if (!candidate.IsAvailable)
                    continue;
                if (!active.HasValue ||
                    candidate.IsProjectedScene == active.Value.IsProjectedScene ||
                    !candidate.IsProjectedScene)
                    active = candidate;
            }
            contextGroups.Add(new InputPromptContextGroup(prompts,
                active?.Prompt));
        }

        List<InputPromptInlineEntry> inlinePrompts = [];
        AddInlinePrompts(_scene, inlinePrompts, mouseInput);

        return new InputPromptLayout(scenePrompts, contextGroups, inlinePrompts);
    }

    readonly record struct ContextCandidate(InputPrompt Prompt, bool IsAvailable,
        bool IsProjectedScene);

    void AddWindowContextCandidates(Window window,
        Dictionary<InputPromptPosition, List<ContextCandidate>> candidates,
        bool mouseInput, bool includeWindowPrompts = true)
    {
        if (!window.IsVisible || IsSuppressed(window.Prompts))
            return;

        if (includeWindowPrompts)
        {
            bool isActive = window.IsPromptActive(_app.LastInputMode);
            foreach (InputPromptEntry entry in window.Prompts.Entries)
            {
                if (entry.IsInline)
                    continue;
                AddEntryContextCandidates(entry, entry.Resolve(), candidates,
                    isActive, mouseInput, isProjectedScene: false);
            }
        }

        if (window is Container container)
            foreach (Window child in container.Windows)
                AddWindowContextCandidates(child, candidates, mouseInput);
    }

    void AddInlinePrompts(Window window, List<InputPromptInlineEntry> prompts,
        bool mouseInput)
    {
        if (!window.IsVisible)
            return;

        if (!IsSuppressed(window.Prompts))
        {
            Vector2 ownerPosition = window.PositionOnScreen - _scene.PositionOnScreen;
            foreach (InputPromptEntry entry in window.Prompts.Entries)
            {
                if (!entry.IsInline)
                    continue;
                InputPrompt? prompt = entry.Resolve();
                if (!prompt.HasValue)
                    continue;
                if (mouseInput && prompt.Value.MouseControl is null or MouseButton.None)
                    continue;

                prompts.Add(new InputPromptInlineEntry(prompt.Value,
                    ownerPosition + entry.Position!.Value,
                    entry.HorizontalAlignment, entry.VerticalAlignment,
                    entry.ShowBackground, entry.HorizontalPadding, entry.Separator));
            }
        }

        if (window is Container container)
            foreach (Window child in container.Windows)
                AddInlinePrompts(child, prompts, mouseInput);
    }

    static void AddEntryContextCandidates(InputPromptEntry entry,
        InputPrompt? activePrompt,
        Dictionary<InputPromptPosition, List<ContextCandidate>> candidates,
        bool ownerIsActive, bool mouseInput, bool isProjectedScene)
    {
        bool activeIsCandidate = false;
        foreach (InputPrompt prompt in entry.Candidates)
        {
            bool isSelected = activePrompt.HasValue && prompt == activePrompt.Value;
            activeIsCandidate |= isSelected;

            MouseButton? mouseControl = ResolveMouseControl(prompt);
            if (mouseInput && !mouseControl.HasValue)
                continue;

            InputPromptPosition role = mouseInput
                ? RoleForMouse(mouseControl!.Value)
                : ContextRole(entry, prompt);
            AddCandidate(candidates, role, new ContextCandidate(prompt,
                ownerIsActive && isSelected, isProjectedScene));
        }

        if (!ownerIsActive || !activePrompt.HasValue || activeIsCandidate)
            return;

        InputPrompt active = activePrompt.Value;
        MouseButton? activeMouseControl = ResolveMouseControl(active);
        if (mouseInput && !activeMouseControl.HasValue)
            return;

        InputPromptPosition activeRole = mouseInput
            ? RoleForMouse(activeMouseControl!.Value)
            : ContextRole(entry, active);
        AddCandidate(candidates, activeRole,
            new ContextCandidate(active, true, isProjectedScene));
    }

    static void AddCandidate(
        Dictionary<InputPromptPosition, List<ContextCandidate>> candidates,
        InputPromptPosition role, ContextCandidate candidate)
    {
        if (!candidates.TryGetValue(role,
            out List<ContextCandidate>? slotCandidates))
        {
            candidates.Add(role, slotCandidates = []);
        }
        slotCandidates.Add(candidate);
    }

    static InputPromptPosition ContextRole(InputPromptEntry entry,
        InputPrompt prompt) => entry.ContextPosition ??
            (prompt.Pattern != InputPattern.None
                ? InputPromptPosition.Extended
                : prompt.Action == InputAction.Primary
                    ? InputPromptPosition.Primary
                    : InputPromptPosition.Secondary);

    static MouseButton? ResolveMouseControl(InputPrompt prompt)
    {
        MouseButton? control = prompt.EffectiveMouseControl;
        return control == MouseButton.None ? null : control;
    }

    static InputPromptPosition RoleForMouse(MouseButton button) => button switch
    {
        MouseButton.Left => InputPromptPosition.Primary,
        MouseButton.Right => InputPromptPosition.Secondary,
        _ => InputPromptPosition.Extended
    };
}
