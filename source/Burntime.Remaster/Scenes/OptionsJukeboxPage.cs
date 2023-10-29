using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Remaster;
using System.Collections.Generic;

namespace Burntime.Classic.Scenes;

internal class OptionsJukeboxPage : Container
{
    readonly OptionFonts _fonts;
    readonly Dictionary<string, Button> _songButtons = new();
    Button _lastPlayingButton;

    public OptionsJukeboxPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;
    }

    public override void OnActivate()
    {
        base.OnActivate();

        CreateSongButtons();
    }

    public override void OnUpdate(float elapsed)
    {
        base.OnUpdate(elapsed);

        _songButtons.TryGetValue(app.Engine.Music.Playing, out Button playingButton);
        if (playingButton is not null)
        {
            playingButton.Font = _fonts.Blue;
            playingButton.HoverFont = _fonts.Orange;
        }

        if (_lastPlayingButton is not null && _lastPlayingButton != playingButton)
        {
            _lastPlayingButton.Font = _fonts.Green;
            _lastPlayingButton.HoverFont = _fonts.Orange;
        }

        _lastPlayingButton = playingButton;
    }

    static string Capitalize(string str)
    {
        var letters = str.ToCharArray();
        letters[0] = char.ToUpper(str[0]);
        return new string(letters);
    }

    void CreateSongButtons()
    {
        foreach (var button in _songButtons.Values)
            Windows -= button;
        _songButtons.Clear();

        int counter = 0;

        foreach (var song in app.Engine.Music.Songlist)
        {
            int y = counter % 8;
            int x = (counter - counter % 8) / 8;

            x = 38 + x * 44;
            y = 58 + y * 10;

            Windows += _songButtons[song] = new Button(app, () => PlaySong(song))
            {
                Position = new Vector2(x, y),
                Text = Capitalize(song),
                Font = _fonts.Green,
                HoverFont = _fonts.Orange,
                IsTextOnly = true
            };

            counter++;
        }
    }

    void PlaySong(string song)
    {
        if (BurntimeClassic.Instance.MusicMode == BurntimeClassic.MusicModes.Off)
            BurntimeClassic.Instance.CycleMusicMode();

        app.Engine.Music.Play(song);
    }
}
