using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Remaster;
using System.Collections.Generic;
using System.Linq;

namespace Burntime.Classic.Scenes;

internal class OptionsJukeboxPage : Container
{
    readonly OptionFonts _fonts;
    readonly Dictionary<string, Button> _songButtons = new();
    Button? _lastPlayingButton;
    Button[] _buttons = [];
    int _selectedIndex;

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

        Button? playingButton = null;
        if (app.Engine.Music.Playing is not null && BurntimeClassic.Instance.MusicMode != BurntimeClassic.MusicModes.Off)
            _songButtons.TryGetValue(app.Engine.Music.Playing, out playingButton);
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

    public void SetKeyboardActive(bool active)
    {
        HasFocus = active;
        UpdateSelection();
    }

    void UpdateSelection()
    {
        for (int i = 0; i < _buttons.Length; i++)
            _buttons[i].IsKeyboardSelected = HasFocus && i == _selectedIndex;
    }

    void MoveVertical(int direction)
    {
        int columnStart = _selectedIndex / 8 * 8;
        int columnCount = System.Math.Min(8, _buttons.Length - columnStart);
        int row = (_selectedIndex % 8 + direction + columnCount) % columnCount;
        _selectedIndex = columnStart + row;
        UpdateSelection();
    }

    bool MoveHorizontal(int direction)
    {
        int row = _selectedIndex % 8;
        int column = _selectedIndex / 8;
        int candidateColumn = column + direction;
        int candidate = candidateColumn * 8 + row;
        if (candidateColumn < 0 || candidate >= _buttons.Length)
            return false;

        _selectedIndex = candidate;
        UpdateSelection();
        return true;
    }

    public override bool OnInputAction(InputAction action)
    {
        if (_buttons.Length == 0)
            return false;

        if (action.IsUp() || action.IsDown())
        {
            MoveVertical(action.IsUp() ? -1 : 1);
            return true;
        }

        if (action.IsLeft() || action.IsRight())
        {
            int direction = action.IsLeft() ? -1 : 1;
            if (!MoveHorizontal(direction) && direction > 0)
                return false;
            return true;
        }

        if (action == InputAction.Primary)
        {
            _buttons[_selectedIndex].OnButtonClick();
            return true;
        }

        return false;
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

        _buttons = _songButtons.Values.ToArray();
        if (_selectedIndex >= _buttons.Length)
            _selectedIndex = 0;
        UpdateSelection();
    }

    void PlaySong(string song)
    {
        if (BurntimeClassic.Instance.MusicMode == BurntimeClassic.MusicModes.Off)
            BurntimeClassic.Instance.CycleMusicMode();

        app.Engine.Music.Play(song);
    }
}
