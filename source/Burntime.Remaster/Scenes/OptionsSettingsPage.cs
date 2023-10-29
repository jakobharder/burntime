using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Remaster;

namespace Burntime.Classic.Scenes;

internal class OptionsSettingsPage : Container
{
    readonly OptionFonts _fonts;

    readonly Button _musicToggle;
    readonly Button _newgfxToggle;
    readonly Button _fullscreenToggle;
    readonly Button _languageToggle;

    readonly Button _hintText;

    public OptionsSettingsPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;

        Windows += _musicToggle = new Button(app, OnMusicToggle)
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Position = new Vector2(38, 68),
            IsTextOnly = true,
            IsEnabled = !BurntimeClassic.Instance.DisableMusic
        };
        Windows += _newgfxToggle = new Button(app, () => app.IsNewGfx = !app.IsNewGfx)
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Position = new Vector2(38, 58),
            IsTextOnly = true
        };
        Windows += _fullscreenToggle = new Button(app, () => app.Engine.IsFullscreen = !app.Engine.IsFullscreen)
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Position = new Vector2(38, 78),
            IsTextOnly = true
        };
        Windows += _languageToggle = new Button(app, () => app.Language = app.Language == "de" ? "en" : "de")
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@newburn?26",
            Position = new Vector2(38, 98),
            IsTextOnly = true
        };
        Windows += _hintText = new Button(app)
        {
            Font = _fonts.Blue,
            Position = new Vector2(40, 122),
            Size = new Vector2(120, 10),
            TextHorizontalAlign = Platform.Graphics.TextAlignment.Center
        };
    }

    public override void OnUpdate(float elapsed)
    {
        // some options can be triggered via key shortcut
        _newgfxToggle.Text = app.IsNewGfx ? "@newburn?17" : "@newburn?18";
        _musicToggle.Text = BurntimeClassic.Instance.MusicMode switch
        {
            BurntimeClassic.MusicModes.Amiga => "@newburn?30",
            BurntimeClassic.MusicModes.Dos => "@newburn?31",
            BurntimeClassic.MusicModes.Remaster => "@newburn?32",
            _ => "@burn?424",
        };
        _fullscreenToggle.Text = app.Engine.IsFullscreen ? "@newburn?19" : "@newburn?20";

        if (_fullscreenToggle.IsHover)
        {
            _hintText.Text = "@newburn?23";
        }
        else if (_newgfxToggle.IsHover)
        {
            _hintText.Text = "@newburn?24";
        }
        else if (_musicToggle.IsHover)
        {
            _hintText.Text = "@newburn?25";
        }
        else
        {
            _hintText.Text = "";
        }

        base.OnUpdate(elapsed);
    }

    string? _lastPlaying;
    void OnMusicToggle()
    {
        if (BurntimeClassic.Instance.DisableMusic) return;

        var mode = BurntimeClassic.Instance.MusicMode;
        if (mode == BurntimeClassic.MusicModes.Off)
        {
            mode = BurntimeClassic.MusicModes.Remaster;
            if (_lastPlaying is not null)
                app.Engine.Music.Play(_lastPlaying);
        }
        else if (mode == BurntimeClassic.MusicModes.Remaster)
            mode = BurntimeClassic.MusicModes.Amiga;
        else if (mode == BurntimeClassic.MusicModes.Amiga)
        {
            _lastPlaying = app.Engine.Music.Playing;
            mode = BurntimeClassic.MusicModes.Off;
        }

        BurntimeClassic.Instance.MusicMode = mode;
    }
}
