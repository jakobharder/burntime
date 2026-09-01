using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.IO;
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
    int _focusIndex;
    BurntimeClassic.MusicModes _displayedMode = (BurntimeClassic.MusicModes)(-1);

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

        if (_displayedMode != BurntimeClassic.Instance.MusicMode)
            CreateSongButtons();

        UpdateFocus();

        Button? playingButton = null;
        if (app.Engine.Music.Playing is not null && BurntimeClassic.Instance.MusicMode != BurntimeClassic.MusicModes.Off)
        {
            string? playingSong = app.Engine.Music.ResolveSong(app.Engine.Music.Playing);
            if (playingSong is not null)
            {
                playingButton = _songButtons
                    .FirstOrDefault(entry => app.Engine.Music.ResolveSong(entry.Key) == playingSong)
                    .Value;
            }
        }
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

    public void SetKeyboardActive(bool active, bool resetFocus = false)
    {
        HasFocus = active;
        if (resetFocus && _buttons.Length > 0)
            _focusIndex = PlayingButtonIndex() is int playingIndex ? playingIndex : 0;
        UpdateFocus();
    }

    int? PlayingButtonIndex()
    {
        if (app.Engine.Music.Playing is null ||
            BurntimeClassic.Instance.MusicMode == BurntimeClassic.MusicModes.Off)
            return null;

        string? playingSong = app.Engine.Music.ResolveSong(app.Engine.Music.Playing);
        if (playingSong is null)
            return null;

        Button? playingButton = _songButtons.FirstOrDefault(entry =>
            app.Engine.Music.ResolveSong(entry.Key) == playingSong).Value;
        int index = System.Array.IndexOf(_buttons, playingButton);
        return index >= 0 ? index : null;
    }

    void UpdateFocus()
    {
        bool keyboardFocus = HasFocus && app.LastInputMode != InputMode.Mouse;
        if (HasFocus && !keyboardFocus)
            _focusIndex = System.Array.FindIndex(_buttons, button => button.IsEnabled && button.IsHover);

        for (int i = 0; i < _buttons.Length; i++)
        {
            if (keyboardFocus && _buttons[i].IsHover)
                _buttons[i].OnMouseLeave();
            _buttons[i].IsKeyboardSelected = keyboardFocus && i == _focusIndex;
        }
    }

    bool PrepareFocusForInput()
    {
        int visibleFocusIndex = System.Array.FindIndex(_buttons, button =>
            button.IsEnabled && (button.IsHover || button.IsKeyboardSelected));
        bool hadVisibleFocus = visibleFocusIndex >= 0 ||
            _focusIndex >= 0 && _focusIndex < _buttons.Length && _buttons[_focusIndex].IsEnabled;
        if (visibleFocusIndex >= 0)
            _focusIndex = visibleFocusIndex;
        else if (!hadVisibleFocus)
            _focusIndex = 0;
        UpdateFocus();
        return hadVisibleFocus;
    }

    void MoveVertical(int direction)
    {
        int columnStart = _focusIndex / 8 * 8;
        int columnCount = System.Math.Min(8, _buttons.Length - columnStart);
        int row = (_focusIndex % 8 + direction + columnCount) % columnCount;
        _focusIndex = columnStart + row;
        UpdateFocus();
    }

    bool MoveHorizontal(int direction)
    {
        int row = _focusIndex % 8;
        int column = _focusIndex / 8;
        int candidateColumn = column + direction;
        int lastColumn = (_buttons.Length - 1) / 8;
        if (candidateColumn < 0 || candidateColumn > lastColumn)
            return false;

        int candidateStart = candidateColumn * 8;
        int candidateCount = Math.Min(8, _buttons.Length - candidateStart);
        _focusIndex = candidateStart + Math.Min(row, candidateCount - 1);
        UpdateFocus();
        return true;
    }

    public override bool OnInputAction(InputAction action)
    {
        if (_buttons.Length == 0)
            return false;

        if (action.IsUp() || action.IsDown())
        {
            PrepareFocusForInput();
            MoveVertical(action.IsUp() ? -1 : 1);
            return true;
        }

        if (action.IsLeft() || action.IsRight())
        {
            PrepareFocusForInput();
            int direction = action.IsLeft() ? -1 : 1;
            if (!MoveHorizontal(direction) && direction > 0)
                return false;
            return true;
        }

        if (action == InputAction.Primary)
        {
            if (!PrepareFocusForInput())
                return true;
            _buttons[_focusIndex].OnButtonClick();
            return true;
        }

        return false;
    }

    void CreateSongButtons()
    {
        foreach (var button in _songButtons.Values)
            Windows -= button;
        _songButtons.Clear();

        BurntimeClassic classic = BurntimeClassic.Instance;
        _displayedMode = classic.MusicMode;
        string jukeboxFile = classic.MusicMode == BurntimeClassic.MusicModes.Amiga
            ? "jukebox_amiga.txt"
            : "jukebox_dos.txt";

        ConfigFile jukebox = new();
        if (!jukebox.Open(jukeboxFile) || jukebox.GetSection("") is not ConfigSection section)
        {
            _buttons = [];
            UpdateFocus();
            return;
        }

        int counter = 0;

        foreach (var entry in section.Values)
        {
            string song = entry.Key;
            int y = counter % 8;
            int x = (counter - counter % 8) / 8;

            x = 38 + x * 44;
            y = 58 + y * 10;

            Windows += _songButtons[song] = new Button(app, () => PlaySong(song))
            {
                Position = new Vector2(x, y),
                Text = entry.Value,
                Font = _fonts.Green,
                HoverFont = _fonts.Orange,
                IsTextOnly = true
            };

            counter++;
        }

        _buttons = _songButtons.Values.ToArray();
        if (_focusIndex >= _buttons.Length)
            _focusIndex = 0;
        UpdateFocus();
    }

    void PlaySong(string song)
    {
        if (BurntimeClassic.Instance.MusicMode == BurntimeClassic.MusicModes.Off)
            BurntimeClassic.Instance.CycleMusicMode();

        app.Engine.Music.Play(song);
    }
}
