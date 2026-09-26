using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster;

/// <summary>
/// A small anchored information panel with optional heading, status and prompts.
/// Text may contain explicit newlines; wrapping is intentionally controlled by
/// the localized resource so translators can choose suitable line breaks.
/// </summary>
public sealed class TooltipWindow : Window
{
    const int HorizontalPadding = 5;
    const int VerticalPadding = 4;
    const int SectionGap = 4;
    const int TextLineAdvance = 10;

    readonly GuiFont _textFont;
    readonly GuiFont _statusFont;
    readonly GuiFont _mutedStatusFont;
    readonly GuiFont _successFont;
    readonly InputControlLabelRenderer _controlRenderer;
    readonly int _extraWidth;

    public GuiString? Header { get; set; }
    public GuiFont HeaderFont { get; set; }
    public GuiString? Text { get; set; }
    public InputPrompt? Prompt { get; set; }
    public InputPrompt? SecondaryPrompt { get; set; }
    public GuiString? Status { get; set; }
    public bool StatusIsMuted { get; set; }
    public bool StatusIsSuccess { get; set; }
    public int MinimumWidth { get; set; } = 100;
    public PixelColor BackgroundColor { get; set; } = new(128, 0, 0, 0);

    public TooltipWindow(Module app, bool neutralPalette = false)
        : base(app)
    {
        HeaderFont = new GuiFont(BurntimeClassic.FontName,
            neutralPalette ? ClassicColors.LightGray : ClassicColors.HudTextHover) { Borders = TextBorders.None };
        _textFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.LightGray) { Borders = TextBorders.None };
        _statusFont = new GuiFont(BurntimeClassic.FontName,
            neutralPalette ? ClassicColors.LightGray : ClassicColors.DialogText) { Borders = TextBorders.None };
        _mutedStatusFont = new GuiFont(BurntimeClassic.FontName,
            neutralPalette ? ClassicColors.LightGray : ClassicColors.StatusInactive) { Borders = TextBorders.None };
        _successFont = new GuiFont(BurntimeClassic.FontName,
            neutralPalette ? ClassicColors.LightGray : ClassicColors.StatusSuccess) { Borders = TextBorders.None };
        // Leave a visible indent for right-aligned status, even when it is the longest line.
        _extraWidth = neutralPalette ? _textFont.GetWidth("MM") : 0;
        var promptFont = new GuiFont(BurntimeClassic.FontName,
            ClassicColors.LightGray) { Borders = TextBorders.None };
        _controlRenderer = new InputControlLabelRenderer(app, promptFont,
            brackets: false, bracketTextControls: false);
        RefreshLayout();
    }

    public void RefreshLayout()
    {
        const int FontHeight = 8;

        string header = Header ?? string.Empty;
        string text = Text ?? string.Empty;
        string status = Status ?? string.Empty;
        bool hasHeader = header.Length > 0;
        bool hasText = text.Length > 0;
        bool hasStatus = status.Length > 0;
        Rect textBounds = hasText ? _textFont.GetRect(0, 0, text) : default;
        int statusWidth = MeasureWidth(_statusFont, status);
        int contentWidth = System.Math.Max(MeasureWidth(HeaderFont, header),
            textBounds.Width);

        bool hasPrompts = Prompt is { IsEmpty: false } ||
            SecondaryPrompt is { IsEmpty: false };
        bool hasFooter = hasStatus || hasPrompts;

        contentWidth = System.Math.Max(contentWidth, statusWidth);
        if (hasPrompts)
        {
            int promptWidth = MeasurePrompt(Prompt);
            int secondaryPromptWidth = MeasurePrompt(SecondaryPrompt);
            int footerWidth = promptWidth + secondaryPromptWidth;
            if (promptWidth > 0 && secondaryPromptWidth > 0)
                footerWidth += 8;
            contentWidth = System.Math.Max(contentWidth, footerWidth);
        }

        int sectionCount = (hasHeader ? 1 : 0) + (hasText ? 1 : 0) +
            (hasFooter ? 1 : 0);
        int height = VerticalPadding * 2 +
            (hasHeader ? TextLineAdvance : 0) + textBounds.Height +
            (hasFooter ? FontHeight : 0) +
            (hasStatus && hasPrompts ? TextLineAdvance : 0) +
            System.Math.Max(0, sectionCount - 1) * SectionGap;

        int width = System.Math.Max(MinimumWidth,
            contentWidth + HorizontalPadding * 2);
        if (_extraWidth > 0)
            width = System.Math.Min(width + _extraWidth, app.Engine.Resolution.Game.x);
        Size = new Vector2(width, height);
        MoveInsideScreen();
    }

    public override void OnRender(RenderTarget target)
    {
        if (app is BurntimeClassic classic && !classic.ShowUIHints)
            return;

        target.RenderRect(Vector2.Zero, Size, BackgroundColor);

        string header = Header ?? string.Empty;
        string text = Text ?? string.Empty;
        string status = Status ?? string.Empty;
        bool hasPrompts = Prompt is { IsEmpty: false } ||
            SecondaryPrompt is { IsEmpty: false };
        bool hasFooter = status.Length > 0 || hasPrompts;
        int y = VerticalPadding;
        if (header.Length > 0)
        {
            HeaderFont.DrawText(target, new Vector2(HorizontalPadding, y), header,
                TextAlignment.Left, VerticalTextAlignment.Top);
            y += TextLineAdvance;
            if (text.Length > 0 || hasFooter)
                y += SectionGap;
        }

        if (text.Length > 0)
        {
            _textFont.DrawText(target, new Vector2(HorizontalPadding, y), text,
                TextAlignment.Left, VerticalTextAlignment.Top);
            y += _textFont.GetRect(0, 0, text).Height;
        }

        if (text.Length > 0 && hasFooter)
            y += SectionGap;
        if (status.Length > 0)
        {
            GuiFont statusFont = StatusIsSuccess ? _successFont :
                StatusIsMuted ? _mutedStatusFont : _statusFont;
            statusFont.DrawText(target,
                new Vector2(Size.x - HorizontalPadding, y), status,
                TextAlignment.Right, VerticalTextAlignment.Top);
            y += TextLineAdvance;
        }
        if (!hasPrompts)
            return;

        InputControlLabel promptControl = InputControlDisplay.Resolve(app,
            app.LastInputMode, Prompt);
        InputControlLabel secondaryPromptControl = InputControlDisplay.Resolve(app,
            app.LastInputMode, SecondaryPrompt);
        bool hasPrompt = !promptControl.IsEmpty;
        bool hasSecondaryPrompt = !secondaryPromptControl.IsEmpty;
        if (hasSecondaryPrompt && SecondaryPrompt is InputPrompt secondaryPrompt)
        {
            int x = hasPrompt ? HorizontalPadding : Size.x - HorizontalPadding;
            _controlRenderer.Draw(target, new Vector2(x, y),
                secondaryPromptControl, secondaryPrompt.Label,
                alignment: hasPrompt ? TextAlignment.Left : TextAlignment.Right,
                labelFirst: true);
        }
        if (hasPrompt && Prompt is InputPrompt prompt)
            _controlRenderer.Draw(target,
                new Vector2(Size.x - HorizontalPadding, y),
                promptControl, prompt.Label, alignment: TextAlignment.Right,
                labelFirst: true);
    }

    static int MeasureWidth(GuiFont font, string text) =>
        text.Length > 0 ? font.GetWidth(text) : 0;

    int MeasurePrompt(InputPrompt? prompt)
    {
        if (prompt is not { IsEmpty: false } value)
            return 0;
        InputControlLabel control = InputControlDisplay.Resolve(app,
            app.LastInputMode, value);
        return control.IsEmpty ? 0 : _controlRenderer.Measure(control, value.Label,
            labelFirst: true);
    }

    void MoveInsideScreen()
    {
        if (Parent == null)
            return;

        Vector2 alignmentOffset = Boundings.Position - Position;
        Vector2 screenPosition = -Parent.PositionOnScreen - alignmentOffset;
        MoveInside(new Rect(screenPosition, app.Engine.Resolution.Game));
    }
}
