using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster;
using Microsoft.VisualBasic;

namespace Burntime.Classic.Scenes;

internal class LanguageScene : Scene
{
    readonly Button _german;
    readonly Button _english;
    readonly GuiFont _hintFont;
    readonly GuiFont _font;
    readonly GuiFont _selectedFont;
    int _selectedLanguage;
    readonly InputPromptOverlay _promptOverlay;

    public LanguageScene(Module app) : base(app)
    {
        var center = app.Engine.Resolution.Game / 2;
        _font = new GuiFont(BurntimeClassic.FontName, PixelColor.White) { Borders = Platform.Graphics.TextBorders.None };
        _selectedFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56)) { Borders = Platform.Graphics.TextBorders.None };

        _hintFont = new GuiFont("highres-font.txt", new PixelColor(128, 128, 128)) { Borders = Platform.Graphics.TextBorders.None };

        Windows += _german = new Button(app, () => SelectLanguage("de"))
        {
            Font = _font,
            HoverFont = _selectedFont,
            Position = center + new Vector2(-5, -10),
            Text = "Deutsch",
            HorizontalAlignment = PositionAlignment.Right,
            IsTextOnly = true
        };

        Windows += _english = new Button(app, () => SelectLanguage("en"))
        {
            Font = _font,
            HoverFont = _selectedFont,
            Position = center + new Vector2(5, -10),
            Text = "English",
            HorizontalAlignment = PositionAlignment.Left,
            IsTextOnly = true
        };

        _selectedLanguage = app.Language == "de" ? 0 : 1;
        Windows += _promptOverlay = new InputPromptOverlay(app);
        _promptOverlay.SetGamepadPrompts(new InputPrompt("A", "@prompts?31"));
        _promptOverlay.SetKeyboardPrompts(new InputPrompt("Enter", "@prompts?31"));
        _promptOverlay.AnchorToScreenBottomRight();
        UpdateSelection();
    }

    public override void OnResizeScreen()
    {
        base.OnResizeScreen();
        _promptOverlay.AnchorToScreenBottomRight();
    }

    void UpdateSelection()
    {
        _german.Font = _selectedLanguage == 0 ? _selectedFont : _font;
        _english.Font = _selectedLanguage == 1 ? _selectedFont : _font;
    }

    public override bool OnInputAction(InputAction action)
    {
        if (action.IsLeft() || action.IsRight())
        {
            _selectedLanguage = 1 - _selectedLanguage;
            UpdateSelection();
            return true;
        }

        if (action == InputAction.Primary)
        {
            SelectLanguage(_selectedLanguage == 0 ? "de" : "en");
            return true;
        }

        return false;
    }
    public override bool OnMouseMove(Vector2 position)
    {
        int hoveredLanguage = _german.IsHover ? 0 : _english.IsHover ? 1 : -1;
        if (hoveredLanguage >= 0 && hoveredLanguage != _selectedLanguage)
        {
            _selectedLanguage = hoveredLanguage;
            UpdateSelection();
        }

        return base.OnMouseMove(position);
    }

    void SelectLanguage(string language)
    {
        app.Language = language;
        app.SceneManager.SetScene("IntroScene");
    }

    public override void OnRender(RenderTarget target)
    {
        base.OnRender(target);

        if (!app.Engine.SupportsFullscreenToggle)
            return;

        var center = app.Engine.Resolution.Game / 2;

        bool showGermanHint = _german.IsHover || (!_english.IsHover && _selectedLanguage == 0);
        _hintFont.DrawText(target, center + new Vector2(0, 20), showGermanHint
            ? "Tipp: drücke F11 für Vollbild"
            : "Hint: use F11 for fullscreen",
            TextAlignment.Center);
    }
}
