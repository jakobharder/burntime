using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster;

sealed partial class InputPromptOverlay
{
    sealed class Row : Window
    {
        const int VerticalPadding = 2;
        static readonly PixelColor HudColor = ClassicColors.HudTextHover;
        static readonly PixelColor OptionsColor = ClassicColors.OptionsRedHover;
        string _separator = "   ";

        readonly bool _smallFont;
        bool UseSmallFontLayout => _smallFont && app.IsNewGfx;
        readonly GuiFont _font;
        readonly InputControlLabelRenderer _controlRenderer;
        readonly Key _preferredPrimaryKeyboardControl;
        readonly List<RowPrompt> _prompts = [];
        PromptDisplay[] _display = [];
        string _language = string.Empty;
        InputMode _inputMode = InputMode.None;
        int _glyphRevision = -1;
        bool _usedSmallFontLayout;
        bool _hasVisiblePrompts;
        int _backgroundLeft;
        int _backgroundWidth;

        readonly record struct PromptDisplay(InputControlLabel Control, string Label,
            int ContentWidth, int ReservedWidth, bool IsVisible);

        public PixelColor BackgroundColor { get; set; } = new(128, 0, 0, 0);
        public bool ShowBackground { get; set; } = true;
        public int HorizontalPadding { get; set; } = 4;
        public string Separator
        {
            get => _separator;
            set
            {
                _separator = value;
                RefreshText();
            }
        }

        public Row(Module app,
            InputPromptColorScheme colors,
            Key preferredPrimaryKeyboardControl, bool smallFont = false)
            : base(app)
        {
            _smallFont = smallFont;
            _preferredPrimaryKeyboardControl = preferredPrimaryKeyboardControl;
            PixelColor textColor = colors switch
            {
                InputPromptColorScheme.Hud => HudColor,
                InputPromptColorScheme.Options => OptionsColor,
                _ => ClassicColors.LightGray
            };
            PixelColor glyphColor = colors switch
            {
                InputPromptColorScheme.Hud => HudColor,
                InputPromptColorScheme.Muted => ClassicColors.LightGray,
                InputPromptColorScheme.Options => OptionsColor,
                _ => PixelColor.White
            };
            _font = new GuiFont(smallFont ? "font-small.txt" : BurntimeClassic.FontName, textColor)
            {
                Borders = TextBorders.None
            };
            _controlRenderer = new InputControlLabelRenderer(app, _font, brackets: false,
                glyphTint: glyphColor, glyphVerticalOffset: smallFont ? 0 : 0.5f);
            HorizontalAlignment = PositionAlignment.Right;
            VerticalAlignment = PositionAlignment.Right;
            RefreshText();
        }

        public void AnchorToScreenBottomRight(int margin = 6)
        {
            Vector2 parentPosition = Parent?.PositionOnScreen ?? Vector2.Zero;
            Position = app.Engine.Resolution.Game - parentPosition - margin;
        }

        public void AnchorToScreenBottomLeft(int margin = 6)
        {
            Vector2 parentPosition = Parent?.PositionOnScreen ?? Vector2.Zero;
            HorizontalAlignment = PositionAlignment.Left;
            VerticalAlignment = PositionAlignment.Right;
            Position = new Vector2(margin - parentPosition.x,
                app.Engine.Resolution.Game.y - parentPosition.y - margin);
        }

        public void SetPrompts(params RowPrompt[] prompts)
        {
            if (PromptsEqual(_prompts, prompts))
                return;

            _prompts.Clear();
            _prompts.AddRange(prompts);
            RefreshText();
        }

        static bool PromptsEqual(List<RowPrompt> current, RowPrompt[] prompts)
        {
            if (current.Count != prompts.Length)
                return false;

            for (int i = 0; i < prompts.Length; i++)
                if (current[i] != prompts[i])
                    return false;

            return true;
        }

        public override void OnRender(RenderTarget target)
        {
            if (app is BurntimeClassic classic && !classic.ShowUIHints)
                return;

            if (app.LastInputMode is not (InputMode.Mouse or InputMode.Keyboard or
                InputMode.Gamepad or InputMode.Touch))
                return;

            if (_inputMode != app.LastInputMode || _language != app.Language ||
                _glyphRevision != app.Engine.InputGlyphs.Revision ||
                _usedSmallFontLayout != UseSmallFontLayout)
            {
                _inputMode = app.LastInputMode;
                RefreshText();
            }
            if (!_hasVisiblePrompts)
                return;

            if (ShowBackground)
                target.RenderRect(new Vector2(_backgroundLeft, 0),
                    new Vector2(_backgroundWidth, Size.y), BackgroundColor);

            // Lay out from right to left. This keeps trailing/global prompts at the
            // exact same pixel when contextual prompts are inserted before them.
            int x = Size.x - RightPadding;
            int separatorWidth = _font.GetWidth(_separator);
            for (int i = _display.Length - 1; i >= 0; i--)
            {
                PromptDisplay display = _display[i];
                if (display.IsVisible)
                {
                    _controlRenderer.Draw(target, new Vector2(x, VerticalPadding + (UseSmallFontLayout ? _controlRenderer.TextOffset : 0)), display.Control,
                        display.Label, alignment: TextAlignment.Right, labelFirst: true);
                }
                x -= display.ReservedWidth + separatorWidth;
            }
        }

        public int MeasurePrompt(InputPrompt prompt)
        {
            InputControlLabel control = InputControlDisplay.Resolve(app,
                app.LastInputMode, prompt, _preferredPrimaryKeyboardControl);
            if (control.IsEmpty)
                return 0;
            return _controlRenderer.Measure(control, prompt.Label, labelFirst: true);
        }

        void RefreshText()
        {
            // RefreshText can run before the first render (for example from
            // SetPrompts). Use the current input mode here so Size/Boundings are
            // already correct when Window.Render creates this window's target.
            _inputMode = app.LastInputMode;

            List<PromptDisplay> display = [];
            int width = 0;
            foreach (RowPrompt rowPrompt in _prompts)
            {
                InputPrompt prompt = rowPrompt.Prompt;
                InputControlLabel control = InputControlDisplay.Resolve(app,
                    _inputMode, prompt, _preferredPrimaryKeyboardControl);
                if (control.IsEmpty)
                    continue;

                string label = prompt.Label;
                int contentWidth = _controlRenderer.Measure(control, label, labelFirst: true);
                int reservedWidth = System.Math.Max(rowPrompt.ReservedWidth, contentWidth);
                display.Add(new PromptDisplay(control, label, contentWidth, reservedWidth,
                    rowPrompt.IsVisible));
                width += reservedWidth;
            }

            _display = display.ToArray();
            _hasVisiblePrompts = display.Exists(prompt => prompt.IsVisible);

            if (_display.Length > 1)
                width += _font.GetWidth(_separator) * (_display.Length - 1);
            _language = app.Language;
            _glyphRevision = app.Engine.InputGlyphs.Revision;
            _usedSmallFontLayout = UseSmallFontLayout;
            Size = new Vector2(
                width + HorizontalPadding + RightPadding,
                (UseSmallFontLayout ? _controlRenderer.LineHeight : _font.GetHeight()) +
                VerticalPadding + (UseSmallFontLayout ? 1 : VerticalPadding));
            UpdateBackgroundBounds();
        }

        // Glyph artwork already contains an inset; avoid doubling it at the right edge.
        int RightPadding => UseSmallFontLayout && _display.Length > 0 &&
            _display[^1].Control.Parts.Count > 0 && _display[^1].Control.Parts[^1].HasGlyph
                ? System.Math.Max(0, HorizontalPadding - 2)
                : HorizontalPadding;

        void UpdateBackgroundBounds()
        {
            int left = Size.x;
            int right = 0;
            int x = Size.x - RightPadding;
            int separatorWidth = _font.GetWidth(_separator);
            for (int i = _display.Length - 1; i >= 0; i--)
            {
                PromptDisplay display = _display[i];
                if (display.IsVisible)
                {
                    left = System.Math.Min(left, x - display.ContentWidth);
                    right = System.Math.Max(right, x);
                }
                x -= display.ReservedWidth + separatorWidth;
            }

            _backgroundLeft = left - HorizontalPadding;
            _backgroundWidth = right - left + HorizontalPadding + RightPadding;
        }
    }
}
