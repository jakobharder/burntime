using Burntime.Platform;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.Logic.Generation;
using Burntime.Platform.IO;
using Burntime.Classic.Scenes;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster;

public class OptionsScene : Scene
{
    GuiFont disabled;
    GuiFont red;
    GuiFont hover;
    GuiFont hoverRed;
    GuiFont green;

    readonly Button _buttonRestart;

    readonly OptionsSavesPage _savesPage;
    readonly OptionsSettingsPage _settingsPage;

    Container _activePage;
    Container ActivePage
    {
        set
        {
            if (_activePage is not null) _activePage.IsVisible = false;
            if (value is not null) value.IsVisible = true;
            _activePage = value;
        }
    }

    public OptionsScene(Module App)
        : base(App)
    {
        Background = "opti.pac";
        Music = "16_MUS 16_HSC.ogg";
        Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

        disabled = new GuiFont(BurntimeClassic.FontName, new PixelColor(100, 100, 100));
        red = new GuiFont(BurntimeClassic.FontName, new PixelColor(134, 44, 4));
        hover = new GuiFont(BurntimeClassic.FontName, new PixelColor(109, 117, 170));
        hoverRed = new GuiFont(BurntimeClassic.FontName, new PixelColor(190, 77, 12));
        green = new GuiFont(BurntimeClassic.FontName, new PixelColor(0, 108, 0));

        Windows += new Image(App)
        {
            Background = "opt.ani",
            Position = new Vector2(0, 4)
        };

        // menu buttons
        Windows += new Button(app, app.SceneManager.PreviousScene)
        {
            Font = red,
            HoverFont = hover,
            Text = "@burn?388",
            Position = new Vector2(214, 64),
            IsTextOnly = true
        };

        Windows += new Button(app, () => ActivePage = _savesPage)
        {
            Font = red,
            HoverFont = hover,
            Text = "@newburn?21",
            Position = new Vector2(214, 84),
            IsTextOnly = true
        };

        Windows += new Button(app, () => ActivePage = _settingsPage)
        {
            Font = red,
            HoverFont = hover,
            Text = "@newburn?22",
            Position = new Vector2(214, 105),
            IsTextOnly = true
        };

        Windows += _buttonRestart = new Button(app, OnButtonRestart)
        {
            Font = red,
            HoverFont = hover,
            Text = "@burn?390",
            Position = new Vector2(214, 127),
            IsTextOnly = true
        };

        Windows += new Button(app, () => app.Close())
        {
            Font = red,
            HoverFont = hover,
            Text = "@burn?391",
            Position = new Vector2(214, 148),
            IsTextOnly = true
        };

        // radio cover
        Windows += new Button(app)
        {
            Image = "opta.raw?0",
            HoverImage = "opta.raw?1",
            Position = new Vector2(186, 51)
        };
        Windows.Last.Layer += 2;

        var fonts = new OptionFonts()
        {
            Disabled = disabled,
            Green = green,
            Blue = hover,
            Orange = hoverRed
        };

        Windows += _savesPage = new OptionsSavesPage(app, fonts) { IsVisible = false };
        Windows += _settingsPage = new OptionsSettingsPage(app, fonts) { IsVisible = false };
        ActivePage = _savesPage;
    }

    public override void OnResizeScreen()
    {
        base.OnResizeScreen();
        Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
    }

    protected override void OnActivateScene(object parameter)
    {
        _savesPage.RefreshSaveGames();

        if (app.SceneManager.LastScene == "MenuScene")
        {
            _buttonRestart.Font = disabled;
            _buttonRestart.HoverFont = null;
        }
        else
        {
            _buttonRestart.Font = red;
            _buttonRestart.HoverFont = hover;
        }
    }

    void OnButtonRestart()
    {
        if (app.SceneManager.LastScene == "MenuScene") return;

        app.StopGame();
        app.SceneManager.SetScene("MenuScene");
    }
}
