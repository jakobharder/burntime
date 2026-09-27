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
    int HorizontalPadding => 5;
    int TopPadding => 4;
    int BottomPadding => app.IsNewGfx ? 2 : 4;
    int SectionGap => app.IsNewGfx ? 2 : 4;
    // Classic text uses 8-pixel glyphs with a 10-pixel line advance.
    int HeaderAdvance => app.IsNewGfx ? HeaderFont.LineHeight : 10;
    int StatusAdvance => app.IsNewGfx ? _statusFont.LineHeight : 10;
    int PromptRowHeight => app.IsNewGfx ? _controlRenderer.LineHeight : 8;
    bool StatusUsesPromptLayout => app.IsNewGfx && StatusReplacesSecondaryPrompt;

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
    public bool StackPrompts { get; set; }
    public bool StatusReplacesSecondaryPrompt { get; set; }
    public InputPrompt? SecondaryPrompt { get; set; }
    InputPrompt? VisibleSecondaryPrompt => StatusReplacesSecondaryPrompt &&
        Status is not null && !string.IsNullOrEmpty(Status) ? null : SecondaryPrompt;
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
        _textFont = new GuiFont("font-small.txt",
            ClassicColors.LightGray) { Borders = TextBorders.None };
        _statusFont = new GuiFont("font-small.txt",
            neutralPalette ? ClassicColors.LightGray : ClassicColors.DialogText) { Borders = TextBorders.None };
        _mutedStatusFont = new GuiFont("font-small.txt",
            neutralPalette ? ClassicColors.LightGray : ClassicColors.StatusInactive) { Borders = TextBorders.None };
        _successFont = new GuiFont("font-small.txt",
            neutralPalette ? ClassicColors.LightGray : ClassicColors.StatusSuccess) { Borders = TextBorders.None };
        // Leave a visible indent for right-aligned status, even when it is the longest line.
        _extraWidth = neutralPalette ? _textFont.GetWidth("MM") : 0;
        var promptFont = new GuiFont("font-small.txt",
            ClassicColors.LightGray) { Borders = TextBorders.None };
        _controlRenderer = new InputControlLabelRenderer(app, promptFont,
            brackets: false, bracketTextControls: false, glyphVerticalOffset: 0);
        RefreshLayout();
    }

    public void RefreshLayout()
    {
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
            VisibleSecondaryPrompt is { IsEmpty: false };
        bool hasFooter = hasStatus || hasPrompts;

        int promptRows = hasPrompts ? 1 : 0;
        contentWidth = System.Math.Max(contentWidth, statusWidth);
        if (hasPrompts)
        {
            int promptWidth = MeasurePrompt(Prompt);
            int secondaryPromptWidth = MeasurePrompt(VisibleSecondaryPrompt);
            int footerWidth;
            if (StackPrompts)
            {
                footerWidth = System.Math.Max(promptWidth, secondaryPromptWidth);
                promptRows = (promptWidth > 0 ? 1 : 0) + (secondaryPromptWidth > 0 ? 1 : 0);
            }
            else
            {
                footerWidth = promptWidth + secondaryPromptWidth;
                if (promptWidth > 0 && secondaryPromptWidth > 0)
                    footerWidth += 2 * SectionGap;
            }
            contentWidth = System.Math.Max(contentWidth, footerWidth);
        }

        if (StatusReplacesSecondaryPrompt && hasStatus)
            promptRows = MeasurePrompt(Prompt) > 0 ? 1 : 0;

        int sectionCount = (hasHeader ? 1 : 0) + (hasText ? 1 : 0) +
            (hasFooter ? 1 : 0);
        int height = TopPadding + BottomPadding +
            (hasHeader ? HeaderAdvance : 0) + TextHeight(text) +
            (hasStatus ? (StatusUsesPromptLayout ? PromptRowHeight :
                app.IsNewGfx ? _statusFont.LineHeight : hasPrompts ? 10 : 8) : 0) +
            promptRows * PromptRowHeight +
            System.Math.Max(0, sectionCount - 1) * SectionGap;

        // A replacement status occupies the same row and bottom inset as a prompt.
        if (app.IsNewGfx && (StatusUsesPromptLayout && hasStatus ||
            !(StatusReplacesSecondaryPrompt && hasStatus) &&
            (StackPrompts && MeasurePrompt(VisibleSecondaryPrompt) > 0
                ? PromptHasGlyph(VisibleSecondaryPrompt)
                : PromptHasGlyph(Prompt) || PromptHasGlyph(VisibleSecondaryPrompt))))
            height--;

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
            VisibleSecondaryPrompt is { IsEmpty: false };
        bool hasFooter = status.Length > 0 || hasPrompts;
        int y = TopPadding;
        if (header.Length > 0)
        {
            HeaderFont.DrawText(target, new Vector2(HorizontalPadding, y), header,
                TextAlignment.Left, VerticalTextAlignment.Top);
            y += HeaderAdvance;
            if (text.Length > 0 || hasFooter)
                y += SectionGap;
        }

        if (text.Length > 0)
        {
            _textFont.DrawText(target, new Vector2(HorizontalPadding, y), text,
                TextAlignment.Left, VerticalTextAlignment.Top);
            y += TextHeight(text);
        }

        if (text.Length > 0 && hasFooter)
            y += SectionGap;
        if (status.Length > 0)
        {
            GuiFont statusFont = StatusIsSuccess ? _successFont :
                StatusIsMuted ? _mutedStatusFont : _statusFont;
            statusFont.DrawText(target,
                new Vector2(Size.x - HorizontalPadding,
                    y + (StatusUsesPromptLayout ? _controlRenderer.TextOffset : 0) +
                    (StatusReplacesSecondaryPrompt && MeasurePrompt(Prompt) > 0
                        ? _controlRenderer.LineHeight : 0)), status,
                TextAlignment.Right, VerticalTextAlignment.Top);
            if (!StatusReplacesSecondaryPrompt)
                y += StatusAdvance;
        }
        if (!hasPrompts)
            return;

        if (app.IsNewGfx)
            y += _controlRenderer.TextOffset;
        InputControlLabel promptControl = InputControlDisplay.Resolve(app,
            app.LastInputMode, Prompt);
        InputControlLabel secondaryPromptControl = InputControlDisplay.Resolve(app,
            app.LastInputMode, VisibleSecondaryPrompt);
        bool hasPrompt = !promptControl.IsEmpty;
        bool hasSecondaryPrompt = !secondaryPromptControl.IsEmpty;
        int right = PromptRightEdge(hasSecondaryPrompt ? secondaryPromptControl : promptControl);
        if (hasSecondaryPrompt && VisibleSecondaryPrompt is InputPrompt secondaryPrompt)
        {
            int secondaryY = y + (StackPrompts && hasPrompt ? _controlRenderer.LineHeight : 0);
            _controlRenderer.Draw(target, new Vector2(right, secondaryY),
                secondaryPromptControl, secondaryPrompt.Label,
                alignment: TextAlignment.Right, labelFirst: true);
            if (StackPrompts)
            {
                right = PromptRightEdge(promptControl);
            }
            else
                right -= MeasurePrompt(SecondaryPrompt) + 2 * SectionGap;
        }
        if (hasPrompt && Prompt is InputPrompt prompt)
        {
            _controlRenderer.Draw(target,
                new Vector2(right, y),
                promptControl, prompt.Label,
                alignment: TextAlignment.Right,
                labelFirst: true);
        }
    }

    int TextHeight(string text) => text.Length == 0 ? 0 :
        text.Split('\n').Length * (app.IsNewGfx ? _textFont.LineHeight : 10);

    int PromptRightEdge(InputControlLabel control)
    {
        // A trailing glyph needs less inset than text to look equally close to the edge.
        bool endsWithGlyph = control.Parts.Count > 0 && control.Parts[^1].HasGlyph;
        int padding = app.IsNewGfx && endsWithGlyph ? BottomPadding : HorizontalPadding;
        return Size.x - padding;
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

    bool PromptHasGlyph(InputPrompt? prompt)
    {
        if (prompt is not { IsEmpty: false } value)
            return false;
        InputControlLabel control = InputControlDisplay.Resolve(app, app.LastInputMode, value);
        foreach (InputControlPart part in control.Parts)
            if (part.HasGlyph)
                return true;
        return false;
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
