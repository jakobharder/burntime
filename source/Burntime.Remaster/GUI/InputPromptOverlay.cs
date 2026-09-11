using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster;

enum InputPromptColorScheme
{
    Default,
    Muted,
    Hud,
    Options
}

sealed partial class InputPromptOverlay : Container
{
    readonly record struct RowPrompt(InputPrompt Prompt, bool IsVisible = true,
        int ReservedWidth = 0);

    readonly InputPromptController _controller;
    readonly InputPromptColorScheme _colors;
    readonly Row _sceneRow;
    readonly Row _contextRow;
    readonly Dictionary<InlineKey, Row> _inlineRows = [];

    readonly record struct InlineKey(int X, int Y,
        PositionAlignment HorizontalAlignment, PositionAlignment VerticalAlignment,
        bool ShowBackground, int HorizontalPadding, string Separator);

    public InputPromptOverlay(Module app,
        InputPromptCollection prompts, InputPromptColorScheme colors)
        : base(app)
    {
        _controller = new InputPromptController(app, prompts);
        _colors = colors;
        Layer = 199;

        Windows += _sceneRow = new Row(app, colors,
            _controller.PreferredPrimaryKeyboardControl);
        _sceneRow.AnchorToScreenBottomLeft();
        Windows += _contextRow = new Row(app, colors,
            _controller.PreferredPrimaryKeyboardControl);
        _contextRow.AnchorToScreenBottomRight();
    }

    public override void OnRender(RenderTarget target)
    {
        Vector2 parentPosition = Parent?.PositionOnScreen ?? Vector2.Zero;
        Position = -parentPosition;
        Size = app.Engine.Resolution.Game;
        Refresh();
    }

    void Refresh()
    {
        _sceneRow.AnchorToScreenBottomLeft();
        _contextRow.AnchorToScreenBottomRight();

        foreach (Row row in _inlineRows.Values)
            row.SetPrompts([]);

        InputPromptLayout layout = _controller.Resolve();
        _sceneRow.SetPrompts(ToVisibleRowPrompts(layout.ScenePrompts));
        _contextRow.SetPrompts(BuildContextPrompts(layout.ContextGroups));
        RefreshInline(layout.InlinePrompts);
    }

    static RowPrompt[] ToVisibleRowPrompts(IReadOnlyList<InputPrompt> prompts)
    {
        RowPrompt[] result = new RowPrompt[prompts.Count];
        for (int i = 0; i < prompts.Count; i++)
            result[i] = new RowPrompt(prompts[i]);
        return result;
    }

    RowPrompt[] BuildContextPrompts(
        IReadOnlyList<InputPromptContextGroup> groups)
    {
        List<RowPrompt> prompts = [];
        foreach (InputPromptContextGroup group in groups)
        {
            InputPrompt widest = default;
            int maximumWidth = 0;
            foreach (InputPrompt candidate in group.Candidates)
            {
                int width = _contextRow.MeasurePrompt(candidate);
                if (width > maximumWidth)
                {
                    maximumWidth = width;
                    widest = candidate;
                }
            }

            InputPrompt display = group.ActivePrompt ?? widest;
            if (display.IsEmpty)
                continue;
            prompts.Add(new RowPrompt(display, group.ActivePrompt.HasValue,
                maximumWidth));
        }
        return prompts.ToArray();
    }

    void RefreshInline(IReadOnlyList<InputPromptInlineEntry> entries)
    {
        Dictionary<InlineKey, List<RowPrompt>> groups = [];
        foreach (InputPromptInlineEntry entry in entries)
        {
            InlineKey key = new(entry.Position.x, entry.Position.y,
                entry.HorizontalAlignment, entry.VerticalAlignment,
                entry.ShowBackground, entry.HorizontalPadding, entry.Separator);
            if (!groups.TryGetValue(key, out List<RowPrompt>? prompts))
                groups.Add(key, prompts = []);
            prompts.Add(new RowPrompt(entry.Prompt));
        }

        HashSet<InlineKey> activeKeys = [.. groups.Keys];
        List<InlineKey> staleKeys = [];
        foreach (InlineKey key in _inlineRows.Keys)
            if (!activeKeys.Contains(key))
                staleKeys.Add(key);
        foreach (InlineKey key in staleKeys)
        {
            Windows -= _inlineRows[key];
            _inlineRows.Remove(key);
        }

        foreach ((InlineKey key, List<RowPrompt> prompts) in groups)
        {
            if (!_inlineRows.TryGetValue(key, out Row? row))
            {
                Windows += row = new Row(app, _colors,
                    _controller.PreferredPrimaryKeyboardControl)
                {
                    HorizontalAlignment = key.HorizontalAlignment,
                    VerticalAlignment = key.VerticalAlignment,
                    ShowBackground = key.ShowBackground,
                    HorizontalPadding = key.HorizontalPadding,
                    Separator = key.Separator
                };
                _inlineRows.Add(key, row);
            }
            Vector2 scenePosition = Parent?.PositionOnScreen ?? Vector2.Zero;
            row.Position = scenePosition + new Vector2(key.X, key.Y);
            row.SetPrompts(prompts.ToArray());
        }
    }
}
