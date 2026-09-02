using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Remaster;
using System;

namespace Burntime.Classic.Scenes;

internal class OptionsSettingsPage : Container
{
    readonly OptionFonts _fonts;

    readonly Button _musicToggle;
    readonly Button _newgfxToggle;
    readonly Button _fullscreenToggle;
    readonly Button _scalingToggle;
    readonly Button _controllerToggle;
    readonly Button _languageToggle;
    readonly Button[] _buttons;
    int _focusIndex;

    readonly Button _hintText;

    public OptionsSettingsPage(Module app, OptionFonts fonts) : base(app)
    {
        _fonts = fonts;

        Windows += _musicToggle = new Button(app, () => BurntimeClassic.Instance.CycleMusicMode())
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Position = new Vector2(38, 88),
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
        Windows += _scalingToggle = new Button(app, () =>
        {
            OutputFiltering previous = app.Engine.OutputFiltering;
            app.Engine.OutputFiltering = NextOutputFiltering(app.Engine);
            if (!app.IsNewGfx &&
                (previous == OutputFiltering.Xbr2 ||
                 app.Engine.OutputFiltering == OutputFiltering.Xbr2))
                app.Engine.ReloadGraphics();
        })
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Position = new Vector2(38, 68),
            IsTextOnly = true
        };
        Windows += _languageToggle = new Button(app,
            () => BurntimeClassic.Instance.CycleLanguageMode())
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Position = new Vector2(38, 108),
            IsTextOnly = true
        };
        Windows += _controllerToggle = new Button(app,
            () => BurntimeClassic.Instance.CycleControllerGlyphMode())
        {
            Font = _fonts.Green,
            HoverFont = _fonts.Orange,
            DisabledFont = _fonts.Disabled,
            Position = new Vector2(38, 98),
            IsTextOnly = true
        };
        if (!app.Engine.SupportsFullscreenToggle)
        {
            _fullscreenToggle.IsEnabled = false;
            _fullscreenToggle.Hide();
        }
        Windows += _hintText = new Button(app)
        {
            Font = _fonts.Blue,
            Position = new Vector2(40, 122),
            Size = new Vector2(120, 10),
            TextHorizontalAlign = Platform.Graphics.TextAlignment.Center
        };

        _buttons = new[]
        {
            _newgfxToggle, _scalingToggle, _fullscreenToggle, _musicToggle,
            _controllerToggle, _languageToggle
        };
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
        UpdateFocus();

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
        _scalingToggle.Text = app.Engine.OutputFiltering switch
        {
            OutputFiltering.NearestPoint => "Scaling POINT",
            OutputFiltering.Linear => "Scaling LINEAR",
            OutputFiltering.Xbr2 => "@newburn?56",
            _ => "@newburn?55"
        };
        _controllerToggle.Text = app.Engine.ControllerGlyphMode switch
        {
            ControllerGlyphMode.Xbox => "@controller?1",
            ControllerGlyphMode.PlayStation => "@controller?2",
            ControllerGlyphMode.Steam => "@controller?3",
            ControllerGlyphMode.Switch => "@controller?4",
            _ => "@controller?0"
        };
        _languageToggle.Text = BurntimeClassic.Instance.LanguageSelection switch
        {
            LanguageMode.English => "@language_mode?1",
            LanguageMode.German => "@language_mode?2",
            _ => "@language_mode?0"
        };

        if (_fullscreenToggle.IsHover || _fullscreenToggle.IsKeyboardSelected)
        {
            _hintText.Text = OperatingSystem.IsMacOS()
                ? "@newburn?57"
                : "@newburn?23";
        }
        else if (_newgfxToggle.IsHover || _newgfxToggle.IsKeyboardSelected)
        {
            _hintText.Text = "@newburn?24";
        }
        else if (_musicToggle.IsHover || _musicToggle.IsKeyboardSelected)
        {
            _hintText.Text = "@newburn?25";
        }
        else
        {
            _hintText.Text = "";
        }

        base.OnUpdate(elapsed);
    }

    static OutputFiltering NextOutputFiltering(IEngine engine)
    {
        if (engine.ForceNearestPointOutputFiltering)
            return OutputFiltering.NearestPoint;
        if (engine.ForceLinearOutputFiltering)
            return OutputFiltering.Linear;
        if (engine.DisableShaders || !engine.SupportsSharpBilinearShader)
            return OutputFiltering.SharpBilinear;
        if (!engine.SupportsXbr2Shader)
            return OutputFiltering.SharpBilinearShader;
        return engine.OutputFiltering == OutputFiltering.Xbr2
            ? OutputFiltering.SharpBilinearShader
            : OutputFiltering.Xbr2;
    }
}
