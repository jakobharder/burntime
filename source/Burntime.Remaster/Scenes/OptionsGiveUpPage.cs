using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;

namespace Burntime.Classic.Scenes;

internal class OptionsGiveUpPage : Container
{
    readonly OptionFonts _fonts;

    readonly Button _buttonRestart;

    public OptionsGiveUpPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;

        Windows += _buttonRestart = new Button(app, OnButtonRestart)
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@newburn?28",
            Position = new Vector2(40, 82),
            Size = new Vector2(120, 10),
            TextHorizontalAlign = Platform.Graphics.TextAlignment.Center
        };
        Windows += new Button(app, () => app.Close())
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?391",
            Position = new Vector2(40, 102),
            Size = new Vector2(120, 10),
            TextHorizontalAlign = Platform.Graphics.TextAlignment.Center
        };
    }

    public override void OnUpdate(float elapsed)
    {
        _buttonRestart.IsEnabled = app.SceneManager.LastScene != "MenuScene";

        base.OnUpdate(elapsed);
    }

    void OnButtonRestart()
    {
        if (app.SceneManager.LastScene == "MenuScene") return;

        app.StopGame();
        app.SceneManager.SetScene("MenuScene");
    }
}
