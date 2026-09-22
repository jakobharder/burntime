using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using System;

namespace Burntime.Classic.Scenes;

internal class OptionsGiveUpPage : Container
{
    readonly OptionFonts _fonts;

    readonly Button _buttonRestart;
    readonly Button _buttonQuit;
    readonly Button[] _buttons;
    int _focusIndex;

    public OptionsGiveUpPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;

        Windows += _buttonRestart = new Button(app, OnButtonRestart)
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@newburn?28",
            Position = new Vector2(100, 82),
            HorizontalAlignment = PositionAlignment.Center,
            IsTextOnly = true
        };
        Windows += _buttonQuit = new Button(app, () => app.Close())
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Text = "@burn?391",
            Position = new Vector2(100, 102),
            HorizontalAlignment = PositionAlignment.Center,
            IsTextOnly = true
        };

        _buttons = new[] { _buttonRestart, _buttonQuit };
    }

    public void SetKeyboardActive(bool active, bool resetFocus = false)
    {
        HasFocus = active;
        if (resetFocus)
            _focusIndex = Array.FindIndex(_buttons, button => button.IsEnabled);
        UpdateFocus();
    }

    void MoveFocus(int direction)
    {
        do
            _focusIndex = (_focusIndex + direction + _buttons.Length) % _buttons.Length;
        while (!_buttons[_focusIndex].IsEnabled);
        UpdateFocus();
    }

    void UpdateFocus()
    {
        bool keyboardFocus = HasFocus && app.LastInputMode != InputMode.Mouse;
        if (HasFocus && !keyboardFocus)
            _focusIndex = Array.FindIndex(_buttons, button => button.IsEnabled && button.IsHover);

        for (int i = 0; i < _buttons.Length; i++)
        {
            if (keyboardFocus && _buttons[i].IsHover)
                _buttons[i].OnMouseLeave();
            _buttons[i].IsKeyboardSelected = keyboardFocus && i == _focusIndex;
        }
    }

    bool PrepareFocusForInput()
    {
        int visibleFocusIndex = Array.FindIndex(_buttons, button =>
            button.IsEnabled && (button.IsHover || button.IsKeyboardSelected));
        bool hadVisibleFocus = visibleFocusIndex >= 0 ||
            _focusIndex >= 0 && _buttons[_focusIndex].IsEnabled;
        if (visibleFocusIndex >= 0)
            _focusIndex = visibleFocusIndex;
        else if (!hadVisibleFocus)
            _focusIndex = Array.FindIndex(_buttons, button => button.IsEnabled);
        UpdateFocus();
        return hadVisibleFocus;
    }

    public override bool OnInputAction(InputAction action)
    {
        if (action.IsUp() || action.IsDown())
        {
            PrepareFocusForInput();
            MoveFocus(action.IsUp() ? -1 : 1);
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

    public override void OnUpdate(float elapsed)
    {
        _buttonRestart.IsEnabled = app.SceneManager.LastScene != "MenuScene";
        if (HasFocus && app.LastInputMode != InputMode.Mouse &&
            (_focusIndex < 0 || !_buttons[_focusIndex].IsEnabled))
        {
            _focusIndex = Array.FindIndex(_buttons, button => button.IsEnabled);
        }
        UpdateFocus();

        base.OnUpdate(elapsed);
    }

    void OnButtonRestart()
    {
        if (app.SceneManager.LastScene == "MenuScene") return;

        app.StopGame();
        app.SceneManager.SetScene("MenuScene");
    }
}
