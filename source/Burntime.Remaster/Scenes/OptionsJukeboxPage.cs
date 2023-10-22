using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Remaster;
using System.Collections.Generic;

namespace Burntime.Classic.Scenes;

internal class OptionsJukeboxPage : Container
{
    readonly OptionFonts _fonts;

    readonly (string, string)[] Songs = {
        ("Intro", "20"),
        ("Start", "15"),
        ("Room", "04"),
        ("Water", "22"),
        ("Diner", "01"),
        ("Doctor", "03"),
        ("Pub", "19"),
        ("Trader", "10"),
        ("NPC", "18"),
        ("Radio", "16"),
        ("Info", "13"),
        ("Ruin", "07"),
        ("Building", "05"),
        ("Texaco", "02"),
        ("Monastery", "14"),
        ("Statistics", "17"),
        ("Hidden", "12"),
        ("Death", "09"),
        ("Victory", "11")
    };

    public OptionsJukeboxPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;
        CreateSongButtons();
    }

    void CreateSongButtons()
    {
        for (int i = 0; i < Songs.Length; i++)
        {
            int y = i % 8;
            int x = (i - i % 8) / 8;

            x = 38 + x * 42;
            y = 58 + y * 10;

            string song = Songs[i].Item2;
            Windows += new Button(app, () => PlaySong(song))
            {
                Position = new Vector2(x, y),
                Text = Songs[i].Item1,
                Font = _fonts.Green,
                HoverFont = _fonts.Orange,
                IsTextOnly = true
            };
        }
    }

    void PlaySong(string song)
    {
        BurntimeClassic.Instance.MusicPlayback = true;
        app.Engine.Music.Enabled = true;
        app.Engine.Music.Play($"{song}_MUS {song}_HSC.ogg");
    }
}
