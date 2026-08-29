using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;

namespace Burntime.Classic.Scenes;

internal class OptionsGiveUpPage : Container
{
    readonly OptionFonts _fonts;

    readonly Button _buttonRestart;
    readonly Button _buttonQuit;
    readonly Button[] _buttons;
    int _selectedIndex;

    public OptionsGiveUpPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;

        Windows += _buttonRestart = new Button(app, OnButtonRestart)
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@newburn?28",
            Position = new Vector2(40, 82),
            Size = new Vector2(120, 10),
            TextHorizontalAlign = Platform.Graphics.TextAlignment.Center
        };
        Windows += _buttonQuit = new Button(app, () => app.Close())
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?391",
            Position = new Vector2(40, 102),
            Size = new Vector2(120, 10),
            TextHorizontalAlign = Platform.Graphics.TextAlignment.Center
        };

        _buttons = new[] { _buttonRestart, _buttonQuit };
    }

    public void SetKeyboardActive(bool active)
    {
        HasFocus = active;
        if (active && !_buttons[_selectedIndex].IsEnabled)
            MoveSelection(1);
        UpdateSelection();
    }

    void MoveSelection(int direction)
    {
        do
            _selectedIndex = (_selectedIndex + direction + _buttons.Length) % _buttons.Length;
        while (!_buttons[_selectedIndex].IsEnabled);
        UpdateSelection();
    }

    void UpdateSelection()
    {
        for (int i = 0; i < _buttons.Length; i++)
            _buttons[i].IsKeyboardSelected = HasFocus && i == _selectedIndex;
    }

    public override bool OnInputAction(InputAction action)
    {
        if (action.IsUp() || action.IsDown())
        {
            MoveSelection(action.IsUp() ? -1 : 1);
            return true;
        }

        if (action == InputAction.Primary)
        {
            _buttons[_selectedIndex].OnButtonClick();
            return true;
        }

        return false;
    }

    public override void OnUpdate(float elapsed)
    {
        _buttonRestart.IsEnabled = app.SceneManager.LastScene != "MenuScene";
        if (HasFocus && !_buttons[_selectedIndex].IsEnabled)
            MoveSelection(1);

        base.OnUpdate(elapsed);
    }

    void OnButtonRestart()
    {
        if (app.SceneManager.LastScene == "MenuScene") return;

        app.StopGame();
        app.SceneManager.SetScene("MenuScene");
    }
}
