using Burntime.Platform;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.Logic.Generation;
using Burntime.Platform.IO;
using Burntime.Classic.Scenes;

namespace Burntime.Remaster;

public class OptionsScene : Scene
{
    GuiFont disabled;
    GuiFont red;
    GuiFont hover;
    GuiFont hoverRed;
    GuiFont green;

    Button music;
    readonly Button _buttonNewGfx;
    readonly Button _buttonRestart;

    readonly OptionsSavesPage _savesPage;

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

        Image image = new Image(App);
        image.Background = "opt.ani";
        image.Position = new Vector2(0, 4);
        Windows += image;

        // menu buttons
        Button button = new Button(App);
        button.Font = red;
        button.HoverFont = hover;
        button.Text = "@burn?388";
        button.Position = new Vector2(214, 64);
        button.IsTextOnly = true;
        button.Command += app.SceneManager.PreviousScene;
        Windows += button;
        button = new Button(App);
        if (BurntimeClassic.Instance.DisableMusic)
        {
            button.Font = disabled;
            button.Text = "@burn?389";
        }
        else
        {
            button.Font = red;
            button.HoverFont = hover;
            button.Text = BurntimeClassic.Instance.MusicPlayback ? "@burn?389" : "@burn?424";
            button.Command += OnButtonMusicSwitch;
        }
        button.Position = new Vector2(214, 84);
        button.IsTextOnly = true;
        music = button;
        Windows += button;

        Windows += _buttonRestart = new Button(App)
        {
            Font = red,
            HoverFont = hover,
            Text = "@burn?390",
            Position = new Vector2(214, 105),
            IsTextOnly = true
        };
        _buttonRestart.Command += OnButtonRestart;

        Windows += _buttonNewGfx = new Button(App)
        {
            Font = red,
            HoverFont = hover,
            Text = "@newburn?17",
            Position = new Vector2(214, 127),
            IsTextOnly = true
        };
        _buttonNewGfx.Command += OnButtonNewGfx;

        button = new Button(App);
        button.Font = red;
        button.HoverFont = hover;
        button.Text = "@burn?391";
        button.Position = new Vector2(214, 148);
        button.IsTextOnly = true;
        button.Command += OnButtonExit;
        Windows += button;

        // radio cover
        button = new Button(App);
        button.Image = "opta.raw?0";
        button.HoverImage = "opta.raw?1";
        button.Position = new Vector2(186, 51);
        button.Layer += 2;
        Windows += button;

        Windows += _savesPage = new OptionsSavesPage(app, new OptionFonts() {
            Disabled = disabled,
            Green = green,
            Blue = hover,
            Orange = hoverRed
        });
        _savesPage.CreateSaveGameButtons();
    }

    public override void OnResizeScreen()
    {
        base.OnResizeScreen();

        Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
    }

    protected override void OnActivateScene(object parameter)
    {
        _savesPage.RefreshSaveGames();
        _buttonNewGfx.Text = BurntimeClassic.Instance.IsNewGfx ? "@newburn?17" : "@newburn?18";

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

    public override void OnUpdate(float Elapsed)
    {
        // can be triggered via F2 or option menu
        var classic = BurntimeClassic.Instance;
        _buttonNewGfx.Text = classic.IsNewGfx ? "@newburn?17" : "@newburn?18";
    }

    void OnButtonMusicSwitch()
    {
        if (!BurntimeClassic.Instance.DisableMusic)
        {
            BurntimeClassic.Instance.MusicPlayback = !BurntimeClassic.Instance.MusicPlayback;
            music.Text = BurntimeClassic.Instance.MusicPlayback ? "@burn?389" : "@burn?424";
            music.IsTextOnly = true;

            if (BurntimeClassic.Instance.MusicPlayback)
            {
                // start music
                app.Engine.Music.Enabled = true;
                app.Engine.Music.Play(Music);
            }
            else
            {
                // stop music
                app.Engine.Music.Enabled = false;
                app.Engine.Music.Stop();
            }
        }
    }

    void OnButtonRestart()
    {
        if (app.SceneManager.LastScene == "MenuScene") return;

        app.StopGame();
        app.SceneManager.SetScene("MenuScene");
    }

    void OnButtonNewGfx()
    {
        var classic = BurntimeClassic.Instance;

        classic.IsNewGfx = !classic.IsNewGfx;
        if (classic.IsNewGfx)
        {
            FileSystem.AddPackage("newgfx", "game/classic_newgfx");
            if (FileSystem.ExistsFile("newgfx.txt"))
            {
                classic.ResourceManager.SetResourceReplacement("newgfx.txt");

                // use highres font anyway
                if (FileSystem.ExistsFile("highres-font.txt"))
                    BurntimeClassic.FontName = "highres-font.txt";
            }
            else
            {
                classic.ResourceManager.SetResourceReplacement(null);
            }
            classic.Engine.ReloadGraphics();
        }
        else
        {
            FileSystem.RemovePackage("newgfx");
            classic.ResourceManager.SetResourceReplacement(null);
            classic.Engine.ReloadGraphics();
        }
    }

    void OnButtonExit()
    {
        app.Close();
    }
}
