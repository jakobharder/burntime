using Burntime.Platform;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.Logic.Generation;
using Burntime.Platform.IO;
using Burntime.Classic.Scenes;
using Burntime.Platform.Graphics;
using System;

namespace Burntime.Remaster;

public class OptionsScene : Scene
{
    GuiFont disabled;
    GuiFont red;
    GuiFont hover;
    GuiFont hoverRed;
    GuiFont green;

    readonly OptionsSavesPage _savesPage;
    readonly OptionsSettingsPage _settingsPage;
    readonly OptionsGiveUpPage _giveUpPage;
    readonly OptionsJukeboxPage _jukeboxPage;

    readonly GuiImage _optionsBulb;
    readonly Image _backgroundAni;

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
        Size = new Vector2(320, 200);
        Music = "radio";
        Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

        disabled = new GuiFont(BurntimeClassic.FontName, new PixelColor(100, 100, 100));
        red = new GuiFont(BurntimeClassic.FontName, new PixelColor(134, 44, 4)) { Borders = TextBorders.None };
        hover = new GuiFont(BurntimeClassic.FontName, new PixelColor(109, 117, 170));
        hoverRed = new GuiFont(BurntimeClassic.FontName, new PixelColor(190, 77, 12));
        green = new GuiFont(BurntimeClassic.FontName, new PixelColor(0, 108, 0));

        _optionsBulb = "gfx/ui/options_bulb.png";

        Windows += _backgroundAni = new Image(App)
        {
            Background = "opt.ani",
            Position = new Vector2(0, 4)
        };
        _backgroundAni.IsVisible = !app.IsNewGfx;

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

        Windows += new Button(app, () => ActivePage = _jukeboxPage)
        {
            Font = red,
            HoverFont = hover,
            DisabledFont = disabled,
            IsEnabled = !BurntimeClassic.Instance.DisableMusic,
            Text = "@newburn?29",
            Position = new Vector2(214, 105),
            IsTextOnly = true
        };

        Windows += new Button(app, () => ActivePage = _settingsPage)
        {
            Font = red,
            HoverFont = hover,
            Text = "@newburn?22",
            Position = new Vector2(214, 127),
            IsTextOnly = true
        };

        Windows += new Button(app, () => ActivePage = _giveUpPage)
        {
            Font = red,
            HoverFont = hover,
            Text = "@newburn?27",
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
        Windows += _giveUpPage = new OptionsGiveUpPage(app, fonts) { IsVisible = false };
        Windows += _jukeboxPage = new OptionsJukeboxPage(app, fonts) { IsVisible = false };
        ActivePage = _savesPage;
    }

    public override void OnResizeScreen()
    {
        base.OnResizeScreen();
        Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        _backgroundAni.IsVisible = !app.IsNewGfx;
    }

    protected override void OnActivateScene(object parameter)
    {
        ActivePage = _savesPage;
        _savesPage.RefreshSaveGames();
    }

    public override void OnRender(RenderTarget target)
    {
        var position = new Vector2(192, 59);
        if (_activePage == _savesPage)
        {
            position.y += 20;
            position.x -= 1;
        }
        else if (_activePage == _jukeboxPage)
            position.y += 20 * 2 + 1;
        else if (_activePage == _settingsPage)
            position.y += 21 * 3;
        else if (_activePage == _giveUpPage)
            position.y += 21 * 4;

        target.Layer++;
        target.DrawSprite(position, _optionsBulb);
        target.Layer--;

        target.Layer += 10;
        red.DrawText(target, target.ScreenSize - target.ScreenOffset - 6, BurntimeClassic.Version, TextAlignment.Right, VerticalTextAlignment.Bottom);
        target.Layer -= 10;

        base.OnRender(target);
    }
}
