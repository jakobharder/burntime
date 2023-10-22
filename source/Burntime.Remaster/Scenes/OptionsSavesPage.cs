using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Remaster;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Classic.Scenes;

internal struct OptionFonts
{
    public GuiFont Disabled;

    public GuiFont Orange;
    public GuiFont Green;
    public GuiFont Blue;
}

internal class OptionsSavesPage : Container
{
    readonly OptionFonts _fonts;

    readonly SavegameInputWindow _input;
    readonly Button _load;
    readonly Button _save;
    readonly Button _delete;

    readonly BurntimeClassic _app;

    readonly Button[] savegames = new Button[8];

    public OptionsSavesPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;
        _app = (BurntimeClassic)app; 

        Windows += _load = new Button(app)
        {
            Font = _fonts.Blue,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?382",
            Position = new Vector2(40, 122),
            IsTextOnly = true
        };
        _load.Command += OnLoad;
        Windows += _save = new Button(app)
        {
            Font = _fonts.Blue,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?383",
            Position = new Vector2(74, 122),
            IsTextOnly = true
        };
        _save.Command += OnSave;
        Windows += _delete = new Button(app)
        {
            Font = _fonts.Blue,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?384",
            Position = new Vector2(126, 122),
            IsTextOnly = true
        };
        _delete.Command += OnDelete;

        Windows += _input = new SavegameInputWindow(app)
        {
            Font = _fonts.Blue,
            Position = new Vector2(40, 108),
            Size = new Vector2(120, 10)
        };
    }

    public override void OnUpdate(float elapsed)
    {
        if (_load.IsHover)
            _input.Mode = SavegameMode.Load;
        else if (_save.IsHover)
            _input.Mode = SavegameMode.Save;
        else if (_delete.IsHover)
            _input.Mode = SavegameMode.Delete;
        else
            _input.Mode = SavegameMode.None;

        _save.IsEnabled = CanSave;
        _load.IsEnabled = CanLoad;
        _delete.IsEnabled = CanDelete;

        base.OnUpdate(elapsed);
    }

    public void RefreshSaveGames()
    {
        _input.Name = "";

        string[] files = FileSystem.GetFileNames("savegame/", ".sav");

        for (int i = 0; i < 8; i++)
        {
            if (files.Length > i)
            {
                var game = new SaveGame("savegame/" + files[i]);

                savegames[i].Text = files[i].ToUpper();
                savegames[i].Font = game.Version == BurntimeClassic.SavegameVersion ? _fonts.Green : _fonts.Disabled;
                savegames[i].IsTextOnly = true;

                game.Close();
            }
            else
                savegames[i].Text = "";
        }
    }

    public void CreateSaveGameButtons()
    {
        for (int i = 0; i < 8; i++)
        {
            int y = i % 4;
            int x = (i - i % 4) / 4;

            x = 38 + x * 67;
            y = 58 + y * 10;

            savegames[i] = new Button(app)
            {
                Position = new Vector2(x, y),
                Text = "",
                Font = _fonts.Green,
                HoverFont = _fonts.Orange,
                IsTextOnly = true
            };
            savegames[i].Command += new CommandHandler(OnSelect, i);
            Windows += savegames[i];
        }
    }

    void OnSelect(int index)
    {
        string str = savegames[index].Text;
        _input.Name = str[..^4];
    }

    private bool CanSave => (!string.IsNullOrEmpty(_input.Name) && app.Server is not null && app.Server.StateContainer is not null);
    void OnSave()
    {
        if (!CanSave)
            return;

        var creation = new GameCreation(app as BurntimeClassic);
        creation.SaveGame("savegame/" + _input.Name + ".sav");

        RefreshSaveGames();
    }

    private bool CanLoad => FileSystem.ExistsFile("savegame/" + _input.Name + ".sav");
    void OnLoad()
    {
        if (!CanLoad)
            return;

        app.SceneManager.SetScene("WaitScene");
        app.SceneManager.BlockBlendIn();

        var creation = new GameCreation(app as BurntimeClassic);
        if (!creation.LoadGame("savegame/" + _input.Name + ".sav"))
            app.SceneManager.PreviousScene();

        app.SceneManager.UnblockBlendIn();
    }

    private bool CanDelete => FileSystem.ExistsFile("savegame/" + _input.Name + ".sav");
    void OnDelete()
    {
        if (!CanDelete)
            return;

        FileSystem.RemoveFile("savegame/" + _input.Name + ".sav");
        RefreshSaveGames();
    }
}
