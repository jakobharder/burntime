using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Remaster;

namespace Burntime.Classic.Scenes;

internal class OptionsJukeboxPage : Container
{
    readonly OptionFonts _fonts;

    public OptionsJukeboxPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;
        CreateSongButtons();
    }

    public override void OnActivate()
    {
        base.OnActivate();

        CreateSongButtons();
    }

    void CreateSongButtons()
    {
        int counter = 0;

        foreach (var song in app.Engine.Music.Songlist)
        {
            int y = counter % 8;
            int x = (counter - counter % 8) / 8;

            x = 38 + x * 42;
            y = 58 + y * 10;

            Windows += new Button(app, () => PlaySong(song.ToLower()))
            {
                Position = new Vector2(x, y),
                Text = song,
                Font = _fonts.Green,
                HoverFont = _fonts.Orange,
                IsTextOnly = true
            };

            counter++;
        }
    }

    void PlaySong(string song)
    {
        BurntimeClassic.Instance.MusicPlayback = true;
        app.Engine.Music.Enabled = true;
        app.Engine.Music.Play(song);
    }
}
