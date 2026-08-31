using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster;
using System;
using System.Globalization;

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
        Size = app.Engine.Resolution.Game;
        var center = app.Engine.Resolution.Game / 2;
        _font = new GuiFont(BurntimeClassic.FontName, new PixelColor(108, 116, 168))
        {
            Borders = TextBorders.None
        };
        _selectedFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 164, 56))
        {
            Borders = TextBorders.None
        };

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

        _selectedLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? 0 : 1;
        Windows += _promptOverlay = new InputPromptOverlay(app);
        _promptOverlay.AnchorToScreenBottomRight();
        UpdateSelection();
    }

    public override void OnResizeScreen()
    {
        base.OnResizeScreen();
        Size = app.Engine.Resolution.Game;
        _promptOverlay.AnchorToScreenBottomRight();
    }

    void UpdateSelection()
    {
        _german.IsKeyboardSelected = _selectedLanguage == 0;
        _english.IsKeyboardSelected = _selectedLanguage == 1;
        bool german = _selectedLanguage == 0;
        _promptOverlay.SetPrompts(
            new InputPrompt(InputAction.MoveLeft, german ? "Sprache" : "Language")
            {
                AlternateAction = InputAction.MoveRight,
                GamepadOverride = "D-pad/Stick Left/Right"
            },
            new InputPrompt(InputAction.Primary, german ? "Auswählen" : "Select"));
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

        string shortcut = OperatingSystem.IsMacOS() ? "Alt+Enter" : "F11";
        _hintFont.DrawText(target, center + new Vector2(0, 20), _selectedLanguage == 0
            ? $"Tipp: drücke {shortcut} für Vollbild"
            : $"Hint: use {shortcut} for fullscreen",
            TextAlignment.Center);
    }
}
