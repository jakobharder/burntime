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
    readonly GuiFont _hintFont;

    public LanguageScene(Module app) : base(app)
    {
        var center = app.Engine.Resolution.Game / 2;
        var font = new GuiFont(BurntimeClassic.FontName, PixelColor.White) { Borders = Platform.Graphics.TextBorders.None };
        var hoverFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(240, 64, 56)) { Borders = Platform.Graphics.TextBorders.None };

        _hintFont = new GuiFont("highres-font_de.txt", new PixelColor(128, 128, 128)) { Borders = Platform.Graphics.TextBorders.None };

        Windows += _german = new Button(app, () => SelectLanguage("de"))
        {
            Font = font,
            HoverFont = hoverFont,
            Position = center + new Vector2(-5, -10),
            Text = "Deutsch",
            HorizontalAlignment = PositionAlignment.Right,
            IsTextOnly = true
        };

        Windows += new Button(app, () => SelectLanguage("en"))
        {
            Font = font,
            HoverFont = hoverFont,
            Position = center + new Vector2(5, -10),
            Text = "English",
            HorizontalAlignment = PositionAlignment.Left,
            IsTextOnly = true
        };
    }

    void SelectLanguage(string language)
    {
        app.Language = language;
        app.SceneManager.SetScene("IntroScene");
    }

    public override void OnRender(RenderTarget target)
    {
        base.OnRender(target);

        var center = app.Engine.Resolution.Game / 2;

        _hintFont.DrawText(target, center + new Vector2(0, 20), _german.IsHover
            ? "Tipp: drücke F11 für Vollbild"
            : "Hint: use F11 for fullscreen",
            TextAlignment.Center);
    }
}
