using Burntime.Platform;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.Logic.Generation;
using Burntime.Platform.IO;
using Burntime.Classic.Scenes;
using Burntime.Platform.Graphics;
using System;

namespace Burntime.Remaster;

public class OptionsScene : Scene
{
    GuiFont disabled;
    GuiFont red;
    GuiFont hover;
    GuiFont hoverRed;
    GuiFont green;

    readonly OptionsSavesPage _savesPage;
    readonly OptionsSettingsPage _settingsPage;
    readonly OptionsGiveUpPage _giveUpPage;
    readonly OptionsJukeboxPage _jukeboxPage;
    readonly Container _emptyPage;
    readonly InputPromptOverlay _promptOverlay;

    readonly GuiImage _optionsBulb;
    readonly Image _backgroundAni;
    readonly Button[] _menuButtons;
    int _menuIndex = 1;
    int _tabFocusIndex = 1;
    bool _tabsFocused;

    Container _activePage;
    Container ActivePage
    {
        set
        {
            if (_activePage is not null) _activePage.IsVisible = false;
            if (value is not null) value.IsVisible = true;
            _activePage = value;
        }
    }

    public OptionsScene(Module App)
        : base(App)
    {
        Background = "opti.pac";
        Size = new Vector2(320, 200);
        Music = "radio";
        Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

        disabled = new GuiFont(BurntimeClassic.FontName, new PixelColor(100, 100, 100)) { Borders = TextBorders.None };
        red = new GuiFont(BurntimeClassic.FontName, new PixelColor(134, 44, 4)) { Borders = TextBorders.None };
        hover = new GuiFont(BurntimeClassic.FontName, new PixelColor(109, 117, 170)) { Borders = TextBorders.None };
        hoverRed = new GuiFont(BurntimeClassic.FontName, new PixelColor(190, 77, 12)) { Borders = TextBorders.None };
        green = new GuiFont(BurntimeClassic.FontName, new PixelColor(0, 108, 0)) { Borders = TextBorders.None };

        _optionsBulb = "gfx/ui/options_bulb.png";

        Windows += _backgroundAni = new Image(App)
        {
            Background = "opt.ani",
            Position = new Vector2(0, 4)
        };
        _backgroundAni.IsVisible = !app.IsNewGfx;

        // menu buttons
        _menuButtons = new Button[5];

        Windows += _menuButtons[0] = new Button(app, app.SceneManager.PreviousScene)
        {
            Font = red,
            HoverFont = hover,
            Text = "@burn?388",
            Position = new Vector2(214, 64),
            IsTextOnly = true
        };

        Windows += _menuButtons[1] = new Button(app, () => SelectPage(1))
        {
            Font = red,
            HoverFont = hover,
            Text = "@newburn?21",
            Position = new Vector2(214, 84),
            IsTextOnly = true
        };

        Windows += _menuButtons[2] = new Button(app, () => SelectPage(2))
        {
            Font = red,
            HoverFont = hover,
            DisabledFont = disabled,
            IsEnabled = !BurntimeClassic.Instance.DisableMusic,
            Text = "@newburn?29",
            Position = new Vector2(214, 105),
            IsTextOnly = true
        };

        Windows += _menuButtons[3] = new Button(app, () => SelectPage(3))
        {
            Font = red,
            HoverFont = hover,
            Text = "@newburn?22",
            Position = new Vector2(214, 127),
            IsTextOnly = true
        };

        Windows += _menuButtons[4] = new Button(app, () => SelectPage(4))
        {
            Font = red,
            HoverFont = hover,
            Text = "@newburn?27",
            Position = new Vector2(214, 148),
            IsTextOnly = true
        };

        // radio cover
        Windows += new Button(app)
        {
            #warning TODO make this fixed? merge it with the background? It doesn't work well with non-mouse input.
            Image = "opta.raw?1",
            HoverImage = "opta.raw?1",
            Position = new Vector2(186, 51)
        };
        Windows.Last.Layer += 2;

        var fonts = new OptionFonts()
        {
            Disabled = disabled,
            Green = green,
            Blue = hover,
            Orange = hoverRed
        };

        Windows += _savesPage = new OptionsSavesPage(app, fonts) { IsVisible = false };
        Windows += _settingsPage = new OptionsSettingsPage(app, fonts) { IsVisible = false };
        Windows += _giveUpPage = new OptionsGiveUpPage(app, fonts) { IsVisible = false };
        Windows += _jukeboxPage = new OptionsJukeboxPage(app, fonts) { IsVisible = false };
        Windows += _emptyPage = new Container(app) { IsVisible = false };
        Windows += _promptOverlay = new InputPromptOverlay(app);
        _promptOverlay.SetPrompts(
            new(InputAction.Primary, "@prompts?31"),
            new(InputAction.LeftArea, "@prompts?30")
            {
                AlternateAction = InputAction.RightArea,
                KeyboardOverride = "Shift+Up/Down",
                PreferredGamepadControl = GamepadControl.LeftShoulder,
                PreferredAlternateGamepadControl = GamepadControl.RightShoulder
            },
            new(InputAction.Back, "@prompts?17"));
        _promptOverlay.AnchorToScreenBottomRight();
        ActivePage = _savesPage;
        UpdatePageFocus();
    }

    void SelectPage(int index)
    {
        _menuIndex = index;
        if (index == 0)
            _tabsFocused = true;
        Container page = index switch
        {
            0 => _emptyPage,
            2 => _jukeboxPage,
            3 => _settingsPage,
            4 => _giveUpPage,
            _ => _savesPage
        };
        bool pageChanged = page != _activePage;
        ActivePage = page;
        UpdatePageFocus(pageChanged);
    }

    void UpdatePageFocus(bool resetActivePageFocus = false)
    {
        bool contentFocused = !_tabsFocused;
        _savesPage.SetKeyboardActive(contentFocused && _activePage == _savesPage,
            resetActivePageFocus && _activePage == _savesPage);
        _settingsPage.SetKeyboardActive(contentFocused && _activePage == _settingsPage,
            resetActivePageFocus && _activePage == _settingsPage);
        _giveUpPage.SetKeyboardActive(contentFocused && _activePage == _giveUpPage,
            resetActivePageFocus && _activePage == _giveUpPage);
        _jukeboxPage.SetKeyboardActive(contentFocused && _activePage == _jukeboxPage,
            resetActivePageFocus && _activePage == _jukeboxPage);

        UpdateTabFocus();
    }

    void UpdateTabFocus()
    {
        bool showKeyboardFocus = _tabsFocused &&
            app.LastInputMode is InputMode.Keyboard or InputMode.Gamepad;
        for (int i = 0; i < _menuButtons.Length; i++)
            _menuButtons[i].IsKeyboardSelected = showKeyboardFocus && i == _tabFocusIndex;
    }

    int MoveIndex(int index, int direction, bool wrap)
    {
        do
        {
            int candidate = index + direction;
            if (wrap)
                candidate = (candidate + _menuButtons.Length) % _menuButtons.Length;
            else if (candidate < 0 || candidate >= _menuButtons.Length)
                return index;

            index = candidate;
        }
        while (!_menuButtons[index].IsEnabled);

        return index;
    }

    void MovePage(int direction, bool wrap)
    {
        int index = MoveIndex(_menuIndex, direction, wrap);
        _tabFocusIndex = index;
        SelectPage(index);
    }

    void MoveTabFocus(int direction)
    {
        int index = MoveIndex(_tabFocusIndex, direction, wrap: false);
        _tabFocusIndex = index;
        SelectPage(index);
    }

    void FocusTabs()
    {
        _tabFocusIndex = _menuIndex;
        _tabsFocused = true;
        UpdatePageFocus();
    }

    void FocusPage()
    {
        if (_menuIndex == 0)
            return;

        _tabsFocused = false;
        UpdatePageFocus();
    }

    public override bool OnInputAction(InputAction action)
    {
        if (action == InputAction.LeftArea || action == InputAction.RightArea)
        {
            MovePage(action == InputAction.LeftArea ? -1 : 1, wrap: true);
            return true;
        }

        if (_tabsFocused)
        {
            if (action.IsUp() || action.IsDown())
            {
                MoveTabFocus(action.IsUp() ? -1 : 1);
                return true;
            }

            if (action.IsLeft())
            {
                SelectPage(_tabFocusIndex);
                FocusPage();
                return true;
            }

            if (action == InputAction.Primary)
            {
                if (_tabFocusIndex == 0)
                    app.SceneManager.PreviousScene();
                else
                {
                    SelectPage(_tabFocusIndex);
                    FocusPage();
                }
                return true;
            }
        }
        else if (action.IsRight())
        {
            // Page controls consume Right while they can move farther right.
            // Reaching the scene therefore means that the true page edge was hit.
            FocusTabs();
            return true;
        }

        if (action == InputAction.Back)
        {
            app.SceneManager.PreviousScene();
            return true;
        }

        return false;
    }

    public override bool TryGetInputAction(Key key, out InputAction action)
    {
        if (key.IsVirtual && (key.Modifier & ModifierKeys.Shift) != 0 &&
            key.VirtualKey is SystemKey.Up or SystemKey.Down)
        {
            // Let OnVKeyPress handle the scene-local page shortcut before the
            // configurable unmodified arrow binding can consume it.
            action = InputAction.None;
            return false;
        }

        if (!_tabsFocused && _activePage == _savesPage && !key.IsVirtual)
        {
            action = InputAction.None;
            return false;
        }

        if (!base.TryGetInputAction(key, out action))
            return false;

        // Keyboard uses Tab for the radio. LeftArea/RightArea remain available
        // here only through the physical gamepad shoulder buttons.
        if (action == InputAction.LeftArea || action == InputAction.RightArea)
        {
            action = InputAction.None;
            return false;
        }

        return true;
    }

    public override bool OnVKeyPress(SystemKey key, ModifierKeys modifier)
    {
        if (key == SystemKey.Tab)
        {
            FocusTabs();
            return true;
        }

        if ((modifier & ModifierKeys.Shift) != 0 && key is SystemKey.Up or SystemKey.Down)
        {
            MovePage(key == SystemKey.Up ? -1 : 1, wrap: true);
            return true;
        }

        return false;
    }

    public override void OnUpdate(float elapsed)
    {
        if (app.LastInputMode == InputMode.Mouse)
        {
            int hoveredIndex = Array.FindIndex(_menuButtons,
                button => button.IsEnabled && button.IsHover);
            if (hoveredIndex >= 0)
                _tabFocusIndex = hoveredIndex;
        }

        UpdateTabFocus();

        base.OnUpdate(elapsed);
    }

    public override void OnResizeScreen()
    {
        base.OnResizeScreen();
        Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        _backgroundAni.IsVisible = !app.IsNewGfx;
        _promptOverlay.AnchorToScreenBottomRight();
    }

    protected override void OnActivateScene(object parameter)
    {
        _menuIndex = 1;
        _tabFocusIndex = 1;
        _tabsFocused = false;
        ActivePage = _savesPage;
        _savesPage.RefreshSaveGames(resetSelection: true);
        UpdatePageFocus(resetActivePageFocus: true);
    }

    public override void OnRender(RenderTarget target)
    {
        var position = new Vector2(192, 59);
        if (_menuIndex == 1)
        {
            position.y += 20;
            position.x -= 1;
        }
        else if (_menuIndex == 2)
            position.y += 20 * 2 + 1;
        else if (_menuIndex == 3)
            position.y += 21 * 3;
        else if (_menuIndex == 4)
            position.y += 21 * 4;

        target.Layer++;
        target.DrawSprite(position, _optionsBulb);
        target.Layer--;

        target.Layer += 10;
        red.DrawText(target, new Vector2(6, target.ScreenSize.y - 6) - target.ScreenOffset,
            BurntimeClassic.Version, TextAlignment.Left, VerticalTextAlignment.Bottom);
        target.Layer -= 10;

        base.OnRender(target);
    }
}
