using System;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster;

/// <summary>
/// A small anchored information panel with an optional heading and input prompt.
/// Text may contain explicit newlines; wrapping is intentionally controlled by
/// the localized resource so translators can choose suitable line breaks.
/// </summary>
public sealed class TooltipWindow : Window
{
    const int HorizontalPadding = 5;
    const int VerticalPadding = 4;
    const int LineGap = 2;
    const int StatusPromptGap = 6;

    readonly GuiFont _headerFont;
    readonly GuiFont _textFont;
    readonly GuiFont _statusFont;
    readonly InputControlLabelRenderer _controlRenderer;
    string _language = string.Empty;
    InputMode _inputMode = InputMode.None;
    int _glyphRevision = -1;
    InputControlLabel _promptControl;
    InputControlLabel _secondaryPromptControl;
    int _promptWidth;
    int _secondaryPromptWidth;
    int _promptGroupWidth;

    public GuiString? Header { get; set; }
    public GuiString? Text { get; set; }
    public InputPrompt? Prompt { get; set; }
    public InputPrompt? SecondaryPrompt { get; set; }
    public GuiString? Status { get; set; }
    public int MinimumWidth { get; set; }
    public Vector2? FixedSize { get; set; }
    public PixelColor BackgroundColor { get; set; } = new(128, 0, 0, 0);

    public TooltipWindow(Module app)
        : base(app)
    {
        _headerFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.HudTextHover) { Borders = TextBorders.None };
        _textFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.LightGray) { Borders = TextBorders.None };
        _statusFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.HudTextAccent) { Borders = TextBorders.None };
        _controlRenderer = new InputControlLabelRenderer(app, _textFont,
            brackets: false, bracketTextControls: false);
        RefreshLayout();
    }

    public override void OnUpdate(float elapsed)
    {
        if (_language != app.Language || _inputMode != app.LastInputMode ||
            _glyphRevision != app.Engine.InputGlyphs.Revision)
            RefreshLayout();
    }

    public void RefreshLayout()
    {
        string header = Header ?? string.Empty;
        string text = Text ?? string.Empty;
        string status = Status ?? string.Empty;
        int statusWidth = _statusFont.GetRect(0, 0, status).Width;
        int contentWidth = System.Math.Max(_headerFont.GetRect(0, 0, header).Width,
            _textFont.GetRect(0, 0, text).Width);

        _promptControl = default;
        _promptWidth = 0;
        if (Prompt is InputPrompt prompt)
        {
            _promptControl = ResolveControl(prompt, app.LastInputMode);
            if (!_promptControl.IsEmpty)
                _promptWidth = _controlRenderer.Measure(_promptControl, prompt.Label);
        }

        _secondaryPromptControl = default;
        _secondaryPromptWidth = 0;
        if (SecondaryPrompt is InputPrompt secondaryPrompt)
        {
            _secondaryPromptControl = ResolveControl(secondaryPrompt, app.LastInputMode);
            if (!_secondaryPromptControl.IsEmpty)
                _secondaryPromptWidth = _controlRenderer.Measure(
                    _secondaryPromptControl, secondaryPrompt.Label);
        }

        _promptGroupWidth = _promptWidth +
            (_promptWidth > 0 && _secondaryPromptWidth > 0 ? StatusPromptGap : 0) +
            _secondaryPromptWidth;

        int footerWidth = statusWidth +
            (statusWidth > 0 && _promptGroupWidth > 0 ? StatusPromptGap : 0) +
            _promptGroupWidth;
        contentWidth = System.Math.Max(contentWidth, footerWidth);

        int height = VerticalPadding * 2;
        if (header.Length > 0)
            height += _headerFont.GetRect(0, 0, header).Height;
        if (header.Length > 0 &&
            (text.Length > 0 || statusWidth > 0 || _promptGroupWidth > 0))
            height += LineGap;
        if (text.Length > 0)
            height += _textFont.GetRect(0, 0, text).Height;
        if (_promptGroupWidth > 0 || status.Length > 0)
        {
            if (text.Length > 0)
                height += LineGap;
            height += System.Math.Max(status.Length > 0 ? _statusFont.GetHeight() : 0,
                _promptGroupWidth > 0 ? _textFont.GetHeight() : 0);
        }

        Size = FixedSize ?? new Vector2(
            System.Math.Max(MinimumWidth, contentWidth + HorizontalPadding * 2),
            height);
        MoveInsideScreen();
        _language = app.Language;
        _inputMode = app.LastInputMode;
        _glyphRevision = app.Engine.InputGlyphs.Revision;
    }

    public override void OnRender(RenderTarget target)
    {
        if (app is BurntimeClassic classic && !classic.ShowUIHints)
            return;

        RefreshLayout();
        target.RenderRect(Vector2.Zero, Size, BackgroundColor);

        string header = Header ?? string.Empty;
        string text = Text ?? string.Empty;
        string status = Status ?? string.Empty;
        int y = VerticalPadding;
        if (header.Length > 0)
        {
            _headerFont.DrawText(target, new Vector2(HorizontalPadding, y), header,
                TextAlignment.Left, VerticalTextAlignment.Top);
            y += _headerFont.GetRect(0, 0, header).Height;
            if (text.Length > 0 || status.Length > 0 || _promptGroupWidth > 0)
                y += LineGap;
        }

        if (text.Length > 0)
        {
            _textFont.DrawText(target, new Vector2(HorizontalPadding, y), text,
                TextAlignment.Left, VerticalTextAlignment.Top);
            y += _textFont.GetRect(0, 0, text).Height;
        }

        if (status.Length > 0 || _promptGroupWidth > 0)
            y += text.Length > 0 ? LineGap : 0;
        if (status.Length > 0)
            _statusFont.DrawText(target, new Vector2(HorizontalPadding, y), status,
                TextAlignment.Left, VerticalTextAlignment.Top);
        if (_promptGroupWidth == 0)
            return;
        int promptX = Size.x - HorizontalPadding - _promptGroupWidth;
        if (_promptWidth > 0 && Prompt is InputPrompt prompt)
        {
            _controlRenderer.Draw(target, new Vector2(promptX, y),
                _promptControl, prompt.Label);
            promptX += _promptWidth +
                (_secondaryPromptWidth > 0 ? StatusPromptGap : 0);
        }
        if (_secondaryPromptWidth > 0 && SecondaryPrompt is InputPrompt secondaryPrompt)
            _controlRenderer.Draw(target, new Vector2(promptX, y),
                _secondaryPromptControl, secondaryPrompt.Label);
    }

    InputControlLabel ResolveControl(InputPrompt prompt, InputMode inputMode)
    {
        InputPattern pattern = inputMode is InputMode.Keyboard or InputMode.Mouse
            ? prompt.KeyboardPattern ?? prompt.Pattern
            : prompt.Pattern;
        return pattern != InputPattern.None
            ? InputControlDisplay.ResolvePattern(app, inputMode, pattern)
            : InputControlDisplay.Resolve(app, inputMode, prompt.Action,
                prompt.KeyboardControl, prompt.GamepadControl,
                prompt.EffectiveMouseControl);
    }

    void MoveInsideScreen()
    {
        if (Parent == null)
            return;

        Vector2 anchorOnScreen = PositionOnScreen;
        Vector2 topLeft = anchorOnScreen + Boundings.Position - Position;
        Vector2 correction = Vector2.Zero;
        Vector2 screen = app.Engine.Resolution.Game;
        if (topLeft.x < 0)
            correction.x = -topLeft.x;
        else if (topLeft.x + Size.x > screen.x)
            correction.x = screen.x - topLeft.x - Size.x;
        if (topLeft.y < 0)
            correction.y = -topLeft.y;
        else if (topLeft.y + Size.y > screen.y)
            correction.y = screen.y - topLeft.y - Size.y;
        if (correction != Vector2.Zero)
            Position += correction;
    }
}
