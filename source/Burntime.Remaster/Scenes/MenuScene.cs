using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.Logic.Generation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Burntime.Remaster;

public class MenuScene : Scene
{
    enum SetupSelection
    {
        Player,
        Load,
        Start,
        Difficulty,
        GameMode,
        AiPlayers,
        Exit
    }

    public const int MAX_FACE_ID = 5;
    private const int MAX_SETUP_FACE_ID = 5;

    SpriteAnimation PlayerOneSlide;
    SpriteAnimation PlayerTwoSlide;
    NameWindow PlayerOneSwitch;
    NameWindow PlayerTwoSwitch;
    bool UsePlayerOne = false;
    bool UsePlayerTwo = false;
    FaceWindow PlayerOneFace;
    FaceWindow PlayerTwoFace;
    Toggle Difficulty;
    Toggle GameMode;
    Toggle AiPlayers;
    Radio Color;
    readonly Radio _otherColor;
    int _currentPlayer;
    SetupSelection _setupSelection;
    readonly Button _loadButton;
    readonly Button _startButton;
    readonly Button _exitButton;
    readonly InputPromptOverlay _promptOverlay;
    Burntime.Platform.IO.ConfigFile conversionTable;
    readonly string[] _playerNames;

    readonly ISprite _copyright;
    readonly GuiFont _playerFont;
    readonly GuiString _playerOne;
    readonly GuiString _playerTwo;
    readonly ISprite _crack1;
    readonly ISprite _crack2;
    readonly ISprite _crack3;
    readonly ISprite _crack4;
    readonly ISprite _crack5;
    readonly ISprite _borderTl;
    readonly ISprite _borderTr;
    readonly ISprite _borderBl;
    readonly ISprite _borderBr;

    readonly GuiFont _infoFont;
    readonly GuiFont _copyrightFont;

    public MenuScene(Module app)
        : base(app)
    {
        Background = "sta.pac";
        Music = "start";
        Size = new Vector2(320, 200);
        Position = (base.app.Engine.Resolution.Game - base.Size) / 2;

        GuiFont buttonFont = new GuiFont("gfx/ui/start_font.txt", PixelColor.Transparent);
        GuiFont selectedNameFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(144, 160, 212));
        _playerFont = new GuiFont("gfx/ui/start_font_player.txt", PixelColor.Transparent);
        _copyrightFont = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.Gray) { Borders = TextBorders.None };
        _infoFont = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.Gray/*new PixelColor(135, 140, 145)*/) { Borders = TextBorders.None };

        _playerOne = "@newburn?43";
        _playerTwo = "@newburn?44";

        _playerNames = new TextResourceFile(Burntime.Platform.IO.FileSystem.GetFile("names.txt"))
            .Data
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _crack1 = app.ResourceManager.GetImage("gfx/start_crack1.png");
        _crack2 = app.ResourceManager.GetImage("gfx/start_crack2.png");
        _crack3 = app.ResourceManager.GetImage("gfx/start_crack3.png");
        _crack4 = app.ResourceManager.GetImage("gfx/start_crack4.png");
        _crack5 = app.ResourceManager.GetImage("gfx/start_crack5.png");
        _borderTl = app.ResourceManager.GetImage("pngsheet@gfx/start_borders.png?0?512x64");
        _borderTr = app.ResourceManager.GetImage("pngsheet@gfx/start_borders.png?1?512x64");
        _borderBl = app.ResourceManager.GetImage("pngsheet@gfx/start_borders.png?2?512x64");
        _borderBr = app.ResourceManager.GetImage("pngsheet@gfx/start_borders.png?3?512x64");
        _copyright = app.ResourceManager.GetImage("gfx/start_maxdesign.png");

        // face
        PlayerOneFace = new FaceWindow(app);
        PlayerOneFace.MaxFaceID = MAX_SETUP_FACE_ID;
        PlayerOneFace.Position = new Vector2(33, 28);
        PlayerOneFace.Group = 3;
        PlayerOneFace.Layer = Layer + 5;
        Windows += PlayerOneFace;
        PlayerTwoFace = new FaceWindow(app);
        PlayerTwoFace.MaxFaceID = MAX_SETUP_FACE_ID;
        PlayerTwoFace.Position = new Vector2(223, 28);
        PlayerTwoFace.Group = 3;
        PlayerTwoFace.Layer = Layer + 5;
        Windows += PlayerTwoFace;

        // face slides
        Image image = new Image(app, "sta.ani?10-16");
        image.Position = new Vector2(30, 26);
        image.Layer += 10;
        PlayerOneSlide = image.Background.Animation;
        Windows += image;
        image = new Image(app, "sta.ani?10-16");
        image.Position = new Vector2(220, 26);
        image.Layer += 10;
        PlayerTwoSlide = image.Background.Animation;
        Windows += image;

        PlayerOneSlide.Speed = 10;
        PlayerTwoSlide.Speed = 10;

        PlayerOneSlide.Endless = false;
        PlayerOneSlide.Stop();
        PlayerOneSlide.GoLastFrame();
        PlayerTwoSlide.Endless = false;
        PlayerTwoSlide.Stop();
        PlayerTwoSlide.GoLastFrame();

        Windows += _startButton = new Button(app, OnButtonStart)
        {
            Position = new Vector2(131, 42),
            Image = "pngsheet@gfx/ui/start_buttons.png?2?64x24",
            HoverImage = "pngsheet@gfx/ui/start_buttons.png?3?64x24",
            Font = buttonFont,
            Text = "@newburn?41",
            TextHorizontalAlign = TextAlignment.Center,
            TextVerticalAlign = VerticalTextAlignment.Center
        };
        Windows += _loadButton = new Button(app, OnButtonLoad)
        {
            Position = new Vector2(131, 15),
            Image = "pngsheet@gfx/ui/start_buttons.png?2?64x24",
            HoverImage = "pngsheet@gfx/ui/start_buttons.png?3?64x24",
            Font = buttonFont,
            Text = "@newburn?40",
            TextHorizontalAlign = TextAlignment.Center,
            TextVerticalAlign = VerticalTextAlignment.Center
        };

        // exit button
        _exitButton = new Button(app, OnButtonExit)
        {
            Image = "gfx/menu_exit.png",
            HoverImage = "gfx/menu_exit_hover.png",
            Position = new Vector2(276, 163)
        };
        Windows += _exitButton;

        // Input prompts are scene-owned: place the reusable overlay once, then
        // replace its contents from UpdateSetupSelection as focus changes.
        Windows += _promptOverlay = new InputPromptOverlay(app);
        UpdatePromptOverlayPosition();

        // player names
        PlayerOneSwitch = new NameWindow(app)
        {
            Position = new Vector2(15, 92),
            Image = "pngsheet@gfx/ui/start_buttons.png?0?112x24",
            DownImage = "pngsheet@gfx/ui/start_buttons.png?1?112x24",
            Size = new Vector2(104, 24),
            Font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.Gray),
            HoverFont = selectedNameFont,
            TextHorizontalAlign = TextAlignment.Center,
            TextVerticalAlign = VerticalTextAlignment.Center
        };
        PlayerOneSwitch.Command += OnPlayerOneClick;
        Windows += PlayerOneSwitch;
        PlayerTwoSwitch = new NameWindow(app)
        {
            Position = new Vector2(204, 92),
            Image = "pngsheet@gfx/ui/start_buttons.png?0?112x24",
            DownImage = "pngsheet@gfx/ui/start_buttons.png?1?112x24",
            Size = new Vector2(104, 24),
            Font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.Gray),
            HoverFont = selectedNameFont,
            TextHorizontalAlign = TextAlignment.Center,
            TextVerticalAlign = VerticalTextAlignment.Center
        };
        PlayerTwoSwitch.Command += OnPlayerTwoClick;
        Windows += PlayerTwoSwitch;

        PlayerOneSwitch.TextInputDeactivated += () => FillEmptyName(PlayerOneSwitch, PlayerTwoSwitch);
        PlayerTwoSwitch.TextInputDeactivated += () => FillEmptyName(PlayerTwoSwitch, PlayerOneSwitch);

        // color
        Radio radio = new Radio(app);
        radio.Position = new Vector2(45, 121);
        radio.Image = "sta.ani?8";
        radio.DownImage = "sta.ani?9";
        radio.Mode = RadioMode.Round;
        radio.Group = 2;
        Color = radio;
        Windows += radio;
        _otherColor = new Radio(app)
        {
            IsDown = true,
            Position = new Vector2(237, 121),
            Image = "sta.ani?8",
            DownImage = "sta.ani?9",
            Mode = RadioMode.Round,
            Group = 2
        };
        Windows += _otherColor;

        // difficulty
        Difficulty = new(app);
        Difficulty.Position = new(100, 149);
        Difficulty.ToolTipFont = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray) { Borders = TextBorders.Screen };
        Difficulty.AddState(null, "gfx/ui/start_button_level1.png", "gfx/ui/start_button_level1_down.png", "gfx/ui/start_button_level1_down.png", "@newburn?14");
        Difficulty.AddState(null, "gfx/ui/start_button_level2.png", "gfx/ui/start_button_level2_down.png", "gfx/ui/start_button_level2_down.png", "@newburn?15");
        Difficulty.AddState(null, "gfx/ui/start_button_level3.png", "gfx/ui/start_button_level3_down.png", "gfx/ui/start_button_level3_down.png", "@newburn?16");
        Windows += Difficulty;

        // mode
        GameMode = new(app);
        GameMode.Position = new(145, 149);
        GameMode.ToolTipFont = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray) { Borders = TextBorders.Screen };
        GameMode.AddState(null, "gfx/ui/start_button_remake.png", "gfx/ui/start_button_remake_down.png", "gfx/ui/start_button_remake_down.png", "@newburn?1");
        GameMode.AddState(null, "gfx/ui/start_button_original.png", "gfx/ui/start_button_original_down.png", "gfx/ui/start_button_original_down.png", "@newburn?0");
        Windows += GameMode;

        // ai
        AiPlayers = new(app);
        AiPlayers.Position = new(190, 149);
        AiPlayers.ToolTipFont = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.LightGray) { Borders = TextBorders.Screen };
        AiPlayers.AddState(null, "gfx/ui/start_button_ai.png", "gfx/ui/start_button_ai_down.png", "gfx/ui/start_button_ai_down.png", "@newburn?12");
        AiPlayers.AddState(null, "gfx/ui/start_button_noai.png", "gfx/ui/start_button_noai_down.png", "gfx/ui/start_button_noai_down.png", "@newburn?13");
        Windows += AiPlayers;

        // input conversion
        conversionTable = new Burntime.Platform.IO.ConfigFile();
        conversionTable.Open(Burntime.Platform.IO.FileSystem.GetFile("conversion_table.txt"));
        PlayerOneSwitch.Table = conversionTable;
        PlayerTwoSwitch.Table = conversionTable;
    }

    public override void OnResizeScreen()
    {
        base.OnResizeScreen();

        Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        UpdatePromptOverlayPosition();
    }

    void UpdatePromptOverlayPosition()
    {
        // Same bottom-right screen anchor as the version label. As a child
        // window, the overlay position is expressed relative to this scene.
        _promptOverlay.AnchorToScreenBottomRight();
    }

    public override void OnRender(RenderTarget target)
    {
        base.OnRender(target);
        if (Background?.IsLoaded != true)
            return;

        target.Layer += 10;
        _infoFont.DrawText(target, target.ScreenSize - target.ScreenOffset - 6, BurntimeClassic.Version, TextAlignment.Right, VerticalTextAlignment.Bottom);
        _copyrightFont.DrawText(target, new Vector2(6, target.ScreenSize.y - 6) - target.ScreenOffset,
            app.IsNewGfx ? "(c) 1993 Max Design. Remastered by Jakob Harder" : "Remastered by Jakob Harder", TextAlignment.Left, VerticalTextAlignment.Bottom);
        target.Layer -= 10;

        if (!app.IsNewGfx)
        {
            //target.Layer += 2;
            //target.DrawSprite(new Vector2(75, 185), _copyright);
            //target.Layer -= 2;
            return;
        }

        Vector2 gameSize = app.Engine.Resolution.Game;
        gameSize.Max(Background.Size);
        Vector2 offset = (app.Engine.Resolution.Game - Background.Size) / 2;
        offset.Min(0);
        offset -= Position;

        //target.Layer += 2;
        //target.DrawSprite(new Vector2(75, gameSize.y - 15 + offset.y), _copyright);
        //target.Layer -= 2;

        target.Layer++;

        const int MARGIN = 3;
        const int MARGINX = 4;

        var lighten = new PixelColor(255 * 2 / 10, 255, 255, 255);
        var darken = new PixelColor(255 * 4 / 10, 0, 0, 0);

        target.RenderRect(offset,
            new Vector2(gameSize.x, MARGIN), lighten);
        target.RenderRect(new Vector2(offset.x, gameSize.y - MARGIN + offset.y),
            new Vector2(gameSize.x, MARGIN + 1), darken);

        target.RenderRect(new Vector2(offset.x, offset.y + MARGIN),
            new Vector2(MARGINX, gameSize.y - MARGIN * 2), lighten);
        target.RenderRect(new Vector2(offset.x + gameSize.x - MARGINX, offset.y + MARGIN),
            new Vector2(MARGINX + 1, gameSize.y - MARGIN * 2), darken);

        target.Layer++;

        target.DrawSprite(offset, _borderTl);
        target.DrawSprite(offset + new Vector2(0, gameSize.y - _borderBl.Height - 1), _borderBl);

        target.Layer++;

        target.DrawSprite(offset + new Vector2(gameSize.x - _borderTr.Width, 0), _borderTr);
        target.DrawSprite(offset + new Vector2(gameSize.x - _borderBr.Width, gameSize.y - _borderBr.Height - 1), _borderBr);

        target.Layer++;

        target.DrawSprite(new Vector2(-15, -2), _crack4);
        target.DrawSprite(new Vector2(204, 15), _crack5);

        _playerFont.DrawText(target, new Vector2(67, 9), _playerOne, TextAlignment.Center, VerticalTextAlignment.Top);
        _playerFont.DrawText(target, new Vector2(255, 9), _playerTwo, TextAlignment.Center, VerticalTextAlignment.Top);

        target.DrawSprite(new Vector2(0, gameSize.y - _crack1.Height) + offset, _crack1);
        target.DrawSprite(new Vector2(gameSize.x - _crack2.Width, 92) + offset, _crack2);
        target.DrawSprite(new Vector2(230, gameSize.y - _crack3.Height + offset.y), _crack3);

    }

    void OnPlayerOneClick()
    {
        PlayerTwoSwitch.IsTextInputActive = false;
        _currentPlayer = 0;
        _setupSelection = SetupSelection.Player;
        if (!PlayerOneSwitch.IsDown && !UsePlayerTwo)
        {
            PlayerOneSwitch.IsDown = true;
            RefreshAutomaticName(PlayerOneSwitch, PlayerTwoSwitch);
            UpdateSetupSelection();
            return;
        }

        PlayerOneSlide.ReverseAnimation = PlayerOneSwitch.IsDown;
        PlayerOneSlide.Start();

        UsePlayerOne = PlayerOneSwitch.IsDown;
        PlayerOneFace.FaceID = UsePlayerOne ? 0 : -1;
        if (!PlayerOneSwitch.HasManualName)
            PlayerOneSwitch.SetAutomaticName(UsePlayerOne ? GetRandomName(PlayerTwoSwitch.Name) : "");
        UpdateSetupSelection();
    }

    void OnPlayerTwoClick()
    {
        PlayerOneSwitch.IsTextInputActive = false;
        _currentPlayer = 1;
        _setupSelection = SetupSelection.Player;
        if (!PlayerTwoSwitch.IsDown && !UsePlayerOne)
        {
            PlayerTwoSwitch.IsDown = true;
            RefreshAutomaticName(PlayerTwoSwitch, PlayerOneSwitch);
            UpdateSetupSelection();
            return;
        }

        PlayerTwoSlide.ReverseAnimation = PlayerTwoSwitch.IsDown;
        PlayerTwoSlide.Start();

        UsePlayerTwo = PlayerTwoSwitch.IsDown;
        PlayerTwoFace.FaceID = UsePlayerTwo ? 0 : -1;
        if (!PlayerTwoSwitch.HasManualName)
            PlayerTwoSwitch.SetAutomaticName(UsePlayerTwo ? GetRandomName(PlayerOneSwitch.Name) : "");
        UpdateSetupSelection();
    }

    void OnButtonLoad()
    {
        app.SceneManager.SetScene("OptionsScene");
    }

    public override bool OnInputAction(InputAction action)
    {
        switch (action)
        {
            case InputAction.LeftArea:
                if (_setupSelection == SetupSelection.Player && CurrentPlayerEnabled)
                    MoveCurrentPlayerFace(-1);
                return true;
            case InputAction.RightArea:
                if (_setupSelection == SetupSelection.Player && CurrentPlayerEnabled)
                    MoveCurrentPlayerFace(1);
                return true;
            case InputAction.Primary:
                if (!HasVisibleSetupSelection())
                {
                    app.LastInputMode = InputMode.Keyboard;
                    UpdateSetupSelection();
                    return true;
                }
                ActivateSetupSelection();
                return true;
            case InputAction.Secondary:
                if (_setupSelection == SetupSelection.Player && CurrentPlayerEnabled)
                    TogglePlayerColors();
                return true;
            case InputAction.MoveUp:
                MoveSetupSelectionUp();
                return true;
            case InputAction.MoveDown:
                MoveSetupSelectionDown();
                return true;
            case InputAction.MoveLeft:
                MoveSetupSelectionHorizontal(-1);
                return true;
            case InputAction.MoveRight:
                MoveSetupSelectionHorizontal(1);
                return true;
            case InputAction.Options:
                OnButtonLoad();
                return true;
            case InputAction.GlobalAction:
                OnButtonStart();
                return true;
            default:
                return false;
        }
    }

    public override bool OnMouseDown(Vector2 position, MouseButton button)
    {
        if (PlayerOneSwitch.IsTextInputActive && !PlayerOneSwitch.Boundings.PointInside(position))
            PlayerOneSwitch.IsTextInputActive = false;
        if (PlayerTwoSwitch.IsTextInputActive && !PlayerTwoSwitch.Boundings.PointInside(position))
            PlayerTwoSwitch.IsTextInputActive = false;

        return base.OnMouseDown(position, button);
    }

    public override InputAction ResolveInputAction(InputAction action) =>
        action == InputAction.Options ? action : base.ResolveInputAction(action);

    public override bool TryGetInputAction(Key key, out InputAction action)
    {
        if (_setupSelection == SetupSelection.Player && key.IsVirtual &&
            key.VirtualKey is SystemKey.Up or SystemKey.Down &&
            (key.Modifier & ModifierKeys.Shift) != 0)
        {
            action = InputAction.Secondary;
            return true;
        }

        if (key.IsVirtual && key.VirtualKey == SystemKey.Escape)
        {
            action = InputAction.Options;
            return true;
        }

        if (!key.IsVirtual)
        {
            action = InputAction.None;
            return false;
        }

        return base.TryGetInputAction(key, out action);
    }

    public override bool OnVKeyPress(SystemKey key, ModifierKeys modifier)
    {
        if (key != SystemKey.Tab)
            return false;

        int direction = (modifier & ModifierKeys.Shift) != 0 ? -1 : 1;
        if (_setupSelection == SetupSelection.Player)
            SelectPlayer((_currentPlayer + direction + 2) % 2, activateName: true);
        else
            SelectPlayer(direction > 0
                ? (UsePlayerOne ? 0 : 1)
                : (UsePlayerTwo ? 1 : 0), activateName: true);
        return true;
    }

    public override bool WantsTextInput => _setupSelection == SetupSelection.Player && !CurrentPlayerEnabled;

    public override bool OnKeyPress(char key)
    {
        if (_setupSelection != SetupSelection.Player || CurrentPlayerEnabled)
            return false;

        NameWindow selectedPlayer = _currentPlayer == 0 ? PlayerOneSwitch : PlayerTwoSwitch;
        if (key == 8 || !selectedPlayer.Font.IsSupportetCharacter(key))
            return true;

        SetCurrentPlayerEnabled(true);
        selectedPlayer.SetAutomaticName("");
        return selectedPlayer.OnKeyPress(key);
    }

    bool IsNameInputActive => PlayerOneSwitch.IsTextInputActive || PlayerTwoSwitch.IsTextInputActive;

    void SelectPlayer(int player, bool activateName)
    {
        _currentPlayer = player;
        _setupSelection = SetupSelection.Player;

        PlayerOneSwitch.IsTextInputActive = activateName && _currentPlayer == 0 && UsePlayerOne;
        PlayerTwoSwitch.IsTextInputActive = activateName && _currentPlayer == 1 && UsePlayerTwo;
        UpdateSetupSelection();
    }

    void SetCurrentPlayerEnabled(bool enabled)
    {
        NameWindow playerSwitch = _currentPlayer == 0 ? PlayerOneSwitch : PlayerTwoSwitch;
        bool isEnabled = _currentPlayer == 0 ? UsePlayerOne : UsePlayerTwo;
        if (isEnabled == enabled)
            return;

        if (!enabled && !OtherPlayerEnabled)
        {
            NameWindow otherPlayerSwitch = _currentPlayer == 0 ? PlayerTwoSwitch : PlayerOneSwitch;
            RefreshAutomaticName(playerSwitch, otherPlayerSwitch);
            return;
        }

        playerSwitch.IsDown = enabled;
        if (_currentPlayer == 0)
            OnPlayerOneClick();
        else
            OnPlayerTwoClick();

        if (enabled)
            ActivateCurrentPlayerName();
        else
            UpdateSetupSelection();
    }

    bool OtherPlayerEnabled => _currentPlayer == 0 ? UsePlayerTwo : UsePlayerOne;
    bool CurrentPlayerEnabled => _currentPlayer == 0 ? UsePlayerOne : UsePlayerTwo;

    void ActivateCurrentPlayerName()
    {
        _setupSelection = SetupSelection.Player;
        PlayerOneSwitch.IsTextInputActive = _currentPlayer == 0 && UsePlayerOne;
        PlayerTwoSwitch.IsTextInputActive = _currentPlayer == 1 && UsePlayerTwo;
        UpdateSetupSelection();
    }

    void SelectStart()
    {
        PlayerOneSwitch.IsTextInputActive = false;
        PlayerTwoSwitch.IsTextInputActive = false;
        _setupSelection = SetupSelection.Start;
        UpdateSetupSelection();
    }

    void UpdateSetupSelection()
    {
        bool showSelection = app.LastInputMode != InputMode.Mouse;
        PlayerOneSwitch.IsKeyboardSelected = showSelection && _setupSelection == SetupSelection.Player && _currentPlayer == 0;
        PlayerTwoSwitch.IsKeyboardSelected = showSelection && _setupSelection == SetupSelection.Player && _currentPlayer == 1;
        _loadButton.IsKeyboardSelected = showSelection && _setupSelection == SetupSelection.Load;
        _startButton.IsKeyboardSelected = showSelection && _setupSelection == SetupSelection.Start;
        Difficulty.IsKeyboardSelected = showSelection && _setupSelection == SetupSelection.Difficulty;
        GameMode.IsKeyboardSelected = showSelection && _setupSelection == SetupSelection.GameMode;
        AiPlayers.IsKeyboardSelected = showSelection && _setupSelection == SetupSelection.AiPlayers;
        _exitButton.IsKeyboardSelected = showSelection && _setupSelection == SetupSelection.Exit;
        UpdatePromptOverlay();
    }

    bool HasVisibleSetupSelection() => _setupSelection switch
    {
        SetupSelection.Player => _currentPlayer == 0
            ? PlayerOneSwitch.IsKeyboardSelected
            : PlayerTwoSwitch.IsKeyboardSelected,
        SetupSelection.Load => _loadButton.IsKeyboardSelected,
        SetupSelection.Start => _startButton.IsKeyboardSelected,
        SetupSelection.Difficulty => Difficulty.IsKeyboardSelected,
        SetupSelection.GameMode => GameMode.IsKeyboardSelected,
        SetupSelection.AiPlayers => AiPlayers.IsKeyboardSelected,
        SetupSelection.Exit => _exitButton.IsKeyboardSelected,
        _ => false
    };

    void UpdatePromptOverlay()
    {
        GuiString primaryLabel = _setupSelection switch
        {
            SetupSelection.Load => "@prompts?3",
            SetupSelection.Start => "@prompts?2",
            SetupSelection.Exit => "@prompts?4",
            SetupSelection.Player when !CurrentPlayerEnabled => "@prompts?8",
            SetupSelection.Player when OtherPlayerEnabled => "@prompts?6",
            SetupSelection.Player => "@prompts?7",
            _ => "@prompts?9"
        };
        InputPrompt primary = new(InputAction.Primary, primaryLabel);

        if (_setupSelection == SetupSelection.Player && CurrentPlayerEnabled)
        {
            List<InputPrompt> prompts =
            [
                new(InputAction.LeftArea, "@prompts?0")
                {
                    AlternateAction = InputAction.RightArea,
                    PreferredKeyboardControl = new Key(SystemKey.Left, ModifierKeys.Shift),
                    PreferredAlternateKeyboardControl = new Key(SystemKey.Right, ModifierKeys.Shift),
                    PreferredGamepadControl = GamepadControl.LeftShoulder,
                    PreferredAlternateGamepadControl = GamepadControl.RightShoulder
                },
                new(InputAction.Secondary, "@prompts?1")
                {
                    KeyboardOverride = "Shift+Up/Down"
                },
                primary
            ];
            if (!IsNameInputActive)
                prompts.Add(new(InputAction.GlobalAction, "@prompts?2"));
            _promptOverlay.SetPrompts(prompts.ToArray());
            return;
        }

        _promptOverlay.SetPrompts(primary, new(InputAction.GlobalAction, "@prompts?2"));
    }

    public override void OnUpdate(float elapsed)
    {
        UpdateSetupSelection();
    }

    void ActivateSetupSelection()
    {
        switch (_setupSelection)
        {
            case SetupSelection.Player:
                SetCurrentPlayerEnabled(!CurrentPlayerEnabled);
                break;
            case SetupSelection.Load:
                OnButtonLoad();
                break;
            case SetupSelection.Start:
                OnButtonStart();
                break;
            case SetupSelection.Difficulty:
                Difficulty.NextState();
                break;
            case SetupSelection.GameMode:
                GameMode.NextState();
                break;
            case SetupSelection.AiPlayers:
                AiPlayers.NextState();
                break;
            case SetupSelection.Exit:
                OnButtonExit();
                break;
        }
    }

    void MoveSetupSelectionUp()
    {
        switch (_setupSelection)
        {
            case SetupSelection.Player:
                SelectStart();
                return;
            case SetupSelection.Start:
                _setupSelection = SetupSelection.Load;
                break;
            case SetupSelection.Difficulty:
            case SetupSelection.GameMode:
                SelectPlayer(0, activateName: true);
                return;
            case SetupSelection.AiPlayers:
            case SetupSelection.Exit:
                SelectPlayer(1, activateName: true);
                return;
            default:
                return;
        }

        UpdateSetupSelection();
    }

    void MoveSetupSelectionDown()
    {
        switch (_setupSelection)
        {
            case SetupSelection.Player:
                PlayerOneSwitch.IsTextInputActive = false;
                PlayerTwoSwitch.IsTextInputActive = false;
                _setupSelection = _currentPlayer == 0
                    ? SetupSelection.Difficulty
                    : SetupSelection.AiPlayers;
                break;
            case SetupSelection.Load:
                _setupSelection = SetupSelection.Start;
                break;
            case SetupSelection.Start:
                SelectPlayer(0, activateName: true);
                return;
            default:
                return;
        }

        UpdateSetupSelection();
    }

    void TogglePlayerColors()
    {
        if (Color.IsDown)
            _otherColor.IsDown = true;
        else
            Color.IsDown = true;
    }

    void MoveSetupSelectionHorizontal(int direction)
    {
        if (_setupSelection == SetupSelection.Player)
        {
            bool movesInward = _currentPlayer == 0 ? direction > 0 : direction < 0;
            if (movesInward)
                SelectPlayer(_currentPlayer == 0 ? 1 : 0, activateName: true);
            return;
        }

        if (_setupSelection is SetupSelection.Load or SetupSelection.Start)
        {
            SelectPlayer(direction < 0 ? 0 : 1, activateName: true);
            return;
        }

        if (_setupSelection is not (SetupSelection.Difficulty or SetupSelection.GameMode or
            SetupSelection.AiPlayers or SetupSelection.Exit))
            return;

        int index = _setupSelection switch
        {
            SetupSelection.Difficulty => 0,
            SetupSelection.GameMode => 1,
            SetupSelection.AiPlayers => 2,
            _ => 3
        };
        index = System.Math.Clamp(index + direction, 0, 3);
        _setupSelection = index switch
        {
            0 => SetupSelection.Difficulty,
            1 => SetupSelection.GameMode,
            2 => SetupSelection.AiPlayers,
            _ => SetupSelection.Exit
        };
        UpdateSetupSelection();
    }

    void RefreshAutomaticName(NameWindow player, NameWindow otherPlayer)
    {
        if (!player.HasManualName)
            player.SetAutomaticName(GetRandomName(otherPlayer.Name, player.Name));
    }

    void FillEmptyName(NameWindow player, NameWindow otherPlayer)
    {
        if (player.IsDown && string.IsNullOrEmpty(player.Name))
            player.SetAutomaticName(GetRandomName(otherPlayer.Name));
    }

    void MoveCurrentPlayerFace(int direction)
    {
        bool isEnabled = _currentPlayer == 0 ? UsePlayerOne : UsePlayerTwo;
        if (!isEnabled)
            return;

        FaceWindow face = _currentPlayer == 0 ? PlayerOneFace : PlayerTwoFace;
        FaceWindow otherFace = _currentPlayer == 0 ? PlayerTwoFace : PlayerOneFace;
        int candidate = face.FaceID;

        do
        {
            candidate = (candidate + direction + MAX_SETUP_FACE_ID + 1) % (MAX_SETUP_FACE_ID + 1);
        }
        while (candidate == otherFace.FaceID);

        face.FaceID = candidate;
    }

    private string GetRandomName(params string?[] excludedNames)
    {
        if (_playerNames.Length == 0)
            return "Max";

        int start = Burntime.Platform.Math.Random.Next(0, _playerNames.Length);
        for (int offset = 0; offset < _playerNames.Length; offset++)
        {
            string candidate = _playerNames[(start + offset) % _playerNames.Length];
            if (!excludedNames.Any(excludedName =>
                candidate.Equals(excludedName, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }

        return _playerNames[start];
    }

    void OnButtonStart()
    {
        if (PlayerOneFace.FaceID == -1 && PlayerTwoFace.FaceID == -1)
            return;

        app.Engine.BlendOverlay.FadeOut();

        GameCreation creation = new GameCreation(app as BurntimeClassic);

        NewGameInfo Info = new()
        {
            NameOne = (string.IsNullOrEmpty(PlayerOneSwitch.Name) && PlayerOneFace.FaceID >= 0) ? GetRandomName(PlayerTwoSwitch.Name) : PlayerOneSwitch.Name,
            NameTwo = (string.IsNullOrEmpty(PlayerTwoSwitch.Name) && PlayerTwoFace.FaceID >= 0) ? GetRandomName(PlayerOneSwitch.Name) : PlayerTwoSwitch.Name,
            FaceOne = PlayerOneFace.FaceID,
            FaceTwo = PlayerTwoFace.FaceID,
            Difficulty = Difficulty.State,
            ColorOne = Color.IsDown ? BurntimePlayerColor.Red : BurntimePlayerColor.Green,
            ColorTwo = Color.IsDown ? BurntimePlayerColor.Green : BurntimePlayerColor.Red,
            ExtendedGame = GameMode.State == 0,
            DisableAI = AiPlayers.State == 1
        };

        creation.CreateNewGame(Info);

        app.SceneManager.SetScene("WaitScene");
    }

    protected override void OnActivateScene(object parameter)
    {
        _currentPlayer = 0;
        _setupSelection = SetupSelection.Player;
        PlayerOneSlide.Stop();
        PlayerOneSlide.GoLastFrame();
        PlayerTwoSlide.Stop();
        PlayerTwoSlide.GoLastFrame();
        PlayerOneFace.FaceID = -1;
        PlayerTwoFace.FaceID = -1;
        UsePlayerOne = false;
        UsePlayerTwo = false;
        PlayerOneSwitch.IsDown = false;
        PlayerOneSwitch.SetAutomaticName("");
        PlayerTwoSwitch.IsDown = false;
        PlayerTwoSwitch.SetAutomaticName("");
        Difficulty.State = 0;
        GameMode.State = 0;
        AiPlayers.State = 0;
        SetCurrentPlayerEnabled(true);
        SelectStart();
    }

    void OnButtonExit()
    {
        app.Close();
    }
}
