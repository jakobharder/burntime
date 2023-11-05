using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Remaster;
using Burntime.Remaster.Logic.Generation;
using System;
using System.Collections.Generic;
using System.Linq;

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
    class SaveInfo
    {
        public string? Name;
        public string? Version;
        public Dictionary<string, string>? Hints;

        public bool IsValid => Version == BurntimeClassic.SavegameVersion && Hints is not null;
    };

    readonly OptionFonts _fonts;

    readonly SavegameInputWindow _input;
    SaveInfo? _highlightedInfo;

    readonly Button _load;
    readonly Button _save;
    readonly Button _delete;
    readonly Button _hintText;

    const int COLUMN_HEIGHT = 5;
    readonly Button[] savegames = new Button[COLUMN_HEIGHT * 2];
    readonly Dictionary<string, SaveInfo> _saveInfos = new ();

    public OptionsSavesPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;

        var saveButtons = new AutoAlignContainer(app)
        {
            Position = new Vector2(40, 124),
            Size = new Vector2(120, 10)
        };
        saveButtons.Windows += _load = new Button(app, OnLoad)
        {
            Font = _fonts.Blue,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?382",
            IsTextOnly = true
        };
        saveButtons.Windows += _save = new Button(app, OnSave)
        {
            Font = _fonts.Blue,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?383",
            IsTextOnly = true
        };
        saveButtons.Windows += _delete = new Button(app, OnDelete)
        {
            Font = _fonts.Blue,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?384",
            IsTextOnly = true
        };
        Windows += saveButtons;

        Windows += _input = new SavegameInputWindow(app)
        {
            Font = _fonts.Blue,
            Position = new Vector2(40, 112),
            Size = new Vector2(120, 10),
            TextHorizontalAlign = Platform.Graphics.TextAlignment.Center,
            TextVerticalAlign = Platform.Graphics.VerticalTextAlignment.Top
        };

        Windows += _hintText = new Button(app)
        {
            Font = _fonts.Blue,
            Position = new Vector2(40, 123),
            Size = new Vector2(120, 10),
            TextHorizontalAlign = Platform.Graphics.TextAlignment.Center,
            TextVerticalAlign = Platform.Graphics.VerticalTextAlignment.Center
        };

        CreateSaveGameButtons();
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

        Button? hovered = savegames.FirstOrDefault(x => x.IsHover);
        if (hovered is not null && hovered.Context is SaveInfo saveInfo)
        {
            if (!saveInfo.IsValid)
            {
                _hintText.Text = "@newburn?37";
            }
            else
            {
                TextHelper txt = new(app, "newburn");
                txt.AddArgument("|J", saveInfo.Hints?.GetValueOrDefault("days") ?? "");
                txt.AddArgument("|A", saveInfo.Hints?.GetValueOrDefault("camps") ?? "");
                _hintText.Text = txt[35] + txt[36];
            }
            _save.IsVisible = false;
            _load.IsVisible = false;
            _delete.IsVisible = false;
        }
        else
        {
            _hintText.Text = "";
            _save.IsVisible = true;
            _load.IsVisible = true;
            _delete.IsVisible = true;
        }

        Button? highlight = savegames.FirstOrDefault(x => (x.Context as SaveInfo)?.Name == _input.Name.ToLower());
        foreach (Button button in savegames)
        {
            if (button == highlight)
            {
                button.Font = _fonts.Blue;
                button.HoverFont = _fonts.Orange;
                _highlightedInfo = button.Context as SaveInfo;
            }
            else if ((button.Context as SaveInfo)?.IsValid == true)
            {
                button.Font = _fonts.Green;
                button.HoverFont = _fonts.Orange;
            }
            else
            {
                button.Font = _fonts.Disabled;
                button.HoverFont = _fonts.Orange;
            }
        }

        base.OnUpdate(elapsed);
    }

    public void RefreshSaveGames(string? changed = null)
    {
        _input.Name = "";

        string[] files = FileSystem.GetFileNames("saves/", ".sav");

        for (int i = 0; i < savegames.Length; i++)
        {
            if (files.Length > i)
            {
                SaveInfo? saveInfo = _saveInfos.GetValueOrDefault(files[i].ToLower());
                if (saveInfo is null || changed?.ToLower() == files[i])
                {
                    saveInfo = new SaveInfo();
                    saveInfo.Name = System.IO.Path.GetFileNameWithoutExtension(files[i]).ToLower();
                    var game = new SaveGame("saves/" + files[i]);
                    saveInfo.Version = game.Version;
                    saveInfo.Hints = game.PeakInfo(app.ResourceManager);
                    game.Close();
                }
                _saveInfos[files[i]] = saveInfo;

                savegames[i].Text = files[i].ToUpper();
                savegames[i].Font = saveInfo.IsValid ? _fonts.Green : _fonts.Disabled;
                savegames[i].IsTextOnly = true;
                savegames[i].HoverFont = saveInfo.IsValid ? _fonts.Orange : _fonts.Disabled;
                savegames[i].Context = saveInfo;
            }
            else
                savegames[i].Text = "";
        }
    }

    void CreateSaveGameButtons()
    {
        int columnHeight = savegames.Length / 2;
        for (int i = 0; i < savegames.Length; i++)
        {
            int y = i % columnHeight;
            int x = (i - i % columnHeight) / columnHeight;

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
        creation.SaveGame("saves/" + _input.Name + ".sav");

        RefreshSaveGames(_input.Name + ".sav");
    }

    private bool CanLoad => FileSystem.ExistsFile("saves/" + _input.Name + ".sav") && _highlightedInfo?.IsValid == true;
    void OnLoad()
    {
        if (!CanLoad)
            return;

        app.SceneManager.SetScene("WaitScene");
        app.SceneManager.BlockBlendIn();

        var creation = new GameCreation(app as BurntimeClassic);
        if (!creation.LoadGame("saves/" + _input.Name + ".sav"))
            app.SceneManager.PreviousScene();

        app.SceneManager.UnblockBlendIn();
    }

    private bool CanDelete => FileSystem.ExistsFile("saves/" + _input.Name + ".sav");
    void OnDelete()
    {
        if (!CanDelete)
            return;

        FileSystem.RemoveFile("saves/" + _input.Name + ".sav");
        RefreshSaveGames();
    }
}
