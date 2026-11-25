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
    readonly List<Row> _sidebarRows = [];
    public int SidebarWidth { get; set; }
    public bool CompactInlinePrompts { get; set; }
    public bool ScenePromptsOnRight { get; set; }
    public int ScenePromptLeftMargin { get; set; } = 6;
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
            _controller.PreferredPrimaryKeyboardControl, smallFont: true);
        _sceneRow.AnchorToScreenBottomLeft();
        Windows += _contextRow = new Row(app, colors,
            _controller.PreferredPrimaryKeyboardControl, smallFont: true);
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
        _sceneRow.Position += new Vector2(ScenePromptLeftMargin - 6, 0);
        _contextRow.AnchorToScreenBottomRight();

        foreach (Row row in _inlineRows.Values)
            row.SetPrompts([]);

        InputPromptLayout layout = _controller.Resolve();
        RowPrompt[] scenePrompts = ToVisibleRowPrompts(layout.ScenePrompts);
        RowPrompt[] contextPrompts = BuildContextPrompts(layout.ContextGroups);
        if (SidebarWidth > 0)
        {
            _sceneRow.SetPrompts([]);
            _contextRow.SetPrompts(contextPrompts);
            RefreshSidebar(scenePrompts);
        }
        else
        {
            foreach (Row row in _sidebarRows) row.SetPrompts([]);
            _sceneRow.SetPrompts(ScenePromptsOnRight ? [] : scenePrompts);
            _contextRow.SetPrompts(ScenePromptsOnRight
                ? [.. contextPrompts, .. scenePrompts] : contextPrompts);
        }
        RefreshInline(layout.InlinePrompts);
    }

    void RefreshSidebar(RowPrompt[] prompts)
    {
        while (_sidebarRows.Count < prompts.Length)
        {
            var row = new Row(app, _colors, _controller.PreferredPrimaryKeyboardControl, smallFont: true)
            {
                HorizontalAlignment = PositionAlignment.Left,
                VerticalAlignment = PositionAlignment.Right,
                HorizontalPadding = 0,
                ShowBackground = false
            };
            Windows += row;
            _sidebarRows.Add(row);
        }
        int bottom = Size.y - 3;
        for (int i = _sidebarRows.Count - 1; i >= 0; i--)
        {
            Row row = _sidebarRows[i];
            row.SetPrompts(i < prompts.Length ? [prompts[i]] : []);
            if (i >= prompts.Length || row.Size.x == 0) continue;
            row.Position = new Vector2(System.Math.Max(4, SidebarWidth - row.Size.x - 4), bottom);
            bottom -= row.Size.y + 1;
        }
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
                row = new Row(app, _colors,
                    _controller.PreferredPrimaryKeyboardControl, smallFont: CompactInlinePrompts)
                {
                    HorizontalAlignment = key.HorizontalAlignment,
                    VerticalAlignment = key.VerticalAlignment,
                    ShowBackground = key.ShowBackground,
                    HorizontalPadding = key.HorizontalPadding,
                    Separator = key.Separator
                };
                Windows += row;
                row.Layer = 199;
                _inlineRows.Add(key, row);
            }
            Vector2 scenePosition = Parent?.PositionOnScreen ?? Vector2.Zero;
            row.Position = scenePosition + new Vector2(key.X, key.Y);
            row.SetPrompts(prompts.ToArray());
        }
    }
}
