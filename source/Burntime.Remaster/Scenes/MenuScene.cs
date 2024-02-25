using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.Logic.Generation;
using System;
using System.Diagnostics;

namespace Burntime.Remaster;

public class MenuScene : Scene
{
    public const int MAX_FACE_ID = 5;

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
    Burntime.Platform.IO.ConfigFile conversionTable;

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
        _playerFont = new GuiFont("gfx/ui/start_font_player.txt", PixelColor.Transparent);
        _copyrightFont = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.Gray) { Borders = TextBorders.None };
        _infoFont = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.Gray/*new PixelColor(135, 140, 145)*/) { Borders = TextBorders.None };

        _playerOne = "@newburn?43";
        _playerTwo = "@newburn?44";

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
        PlayerOneFace.MaxFaceID = MAX_FACE_ID;
        PlayerOneFace.Position = new Vector2(33, 28);
        PlayerOneFace.Group = 3;
        PlayerOneFace.Layer = Layer + 5;
        Windows += PlayerOneFace;
        PlayerTwoFace = new FaceWindow(app);
        PlayerTwoFace.MaxFaceID = 5;
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

        Windows += new Button(app, OnButtonStart)
        {
            Position = new Vector2(131, 42),
            Image = "pngsheet@gfx/ui/start_buttons.png?2?64x24",
            HoverImage = "pngsheet@gfx/ui/start_buttons.png?3?64x24",
            Font = buttonFont,
            Text = "@newburn?41",
            TextHorizontalAlign = TextAlignment.Center,
            TextVerticalAlign = VerticalTextAlignment.Center
        };
        Windows += new Button(app, OnButtonLoad)
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
        Button button = new Button(app);
        button.Image = "gfx/menu_exit.png";
        button.HoverImage = "gfx/menu_exit_hover.png";
        button.Position = new Vector2(276, 163);
        button.Command += OnButtonExit;
        Windows += button;

        // player names
        PlayerOneSwitch = new NameWindow(app)
        {
            Position = new Vector2(15, 92),
            Image = "pngsheet@gfx/ui/start_buttons.png?0?112x24",
            DownImage = "pngsheet@gfx/ui/start_buttons.png?1?112x24",
            Size = new Vector2(104, 24),
            Font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.Gray),
            TextHorizontalAlign = TextAlignment.Center,
            TextVerticalAlign = VerticalTextAlignment.Center
        };
        PlayerOneSwitch.DownCommand += OnPlayerOneDown;
        PlayerOneSwitch.UpCommand += OnPlayerOneUp;
        PlayerOneSwitch.Command += OnPlayerOneClick;
        Windows += PlayerOneSwitch;
        PlayerTwoSwitch = new NameWindow(app)
        {
            Position = new Vector2(204, 92),
            Image = "pngsheet@gfx/ui/start_buttons.png?0?112x24",
            DownImage = "pngsheet@gfx/ui/start_buttons.png?1?112x24",
            Size = new Vector2(104, 24),
            Font = new GuiFont(BurntimeClassic.FontName, BurntimeClassic.Gray),
            TextHorizontalAlign = TextAlignment.Center,
            TextVerticalAlign = VerticalTextAlignment.Center
        };
        PlayerTwoSwitch.DownCommand += OnPlayerTwoDown;
        PlayerTwoSwitch.UpCommand += OnPlayerTwoUp;
        PlayerTwoSwitch.Command += OnPlayerTwoClick;
        Windows += PlayerTwoSwitch;

        // color
        Radio radio = new Radio(app);
        radio.Position = new Vector2(45, 121);
        radio.Image = "sta.ani?8";
        radio.DownImage = "sta.ani?9";
        radio.Mode = RadioMode.Round;
        radio.Group = 2;
        Color = radio;
        Windows += radio;
        radio = new Radio(app);
        radio.IsDown = true;
        radio.Position = new Vector2(237, 121);
        radio.Image = "sta.ani?8";
        radio.DownImage = "sta.ani?9";
        radio.Mode = RadioMode.Round;
        radio.Group = 2;
        Windows += radio;

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

    void OnPlayerOneDown()
    {
        if (PlayerTwoSwitch.IsDown)
            PlayerTwoSwitch.IsDown = false;
    }

    void OnPlayerOneUp()
    {
        if (UsePlayerTwo)
            PlayerTwoSwitch.IsDown = true;
    }

    void OnPlayerOneClick()
    {
        PlayerOneSlide.ReverseAnimation = PlayerOneSwitch.IsDown;
        PlayerOneSlide.Start();

        UsePlayerOne = PlayerOneSwitch.IsDown;
        PlayerOneFace.FaceID = UsePlayerOne ? 0 : -1;
        if (!UsePlayerOne)
            PlayerOneSwitch.Name = "";
    }

    void OnPlayerTwoDown()
    {
        if (PlayerOneSwitch.IsDown)
            PlayerOneSwitch.IsDown = false;
    }

    void OnPlayerTwoUp()
    {
        if (UsePlayerOne)
            PlayerOneSwitch.IsDown = true;
    }

    void OnPlayerTwoClick()
    {
        PlayerTwoSlide.ReverseAnimation = PlayerTwoSwitch.IsDown;
        PlayerTwoSlide.Start();

        UsePlayerTwo = PlayerTwoSwitch.IsDown;
        PlayerTwoFace.FaceID = UsePlayerTwo ? 0 : -1;
        if (!UsePlayerTwo)
            PlayerTwoSwitch.Name = "";
    }

    void OnButtonLoad()
    {
        app.SceneManager.SetScene("OptionsScene");
    }

    private string GetRandomName()
    {
        return "Max";
    }

    void OnButtonStart()
    {
        if (PlayerOneFace.FaceID == -1 && PlayerTwoFace.FaceID == -1)
            return;

        app.Engine.BlendOverlay.FadeOut();

        GameCreation creation = new GameCreation(app as BurntimeClassic);

        NewGameInfo Info = new()
        {
            NameOne = (string.IsNullOrEmpty(PlayerOneSwitch.Name) && PlayerOneFace.FaceID >= 0) ? GetRandomName() : PlayerOneSwitch.Name,
            NameTwo = (string.IsNullOrEmpty(PlayerTwoSwitch.Name) && PlayerTwoFace.FaceID >= 0) ? GetRandomName() : PlayerTwoSwitch.Name,
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
        PlayerOneSlide.Stop();
        PlayerOneSlide.GoLastFrame();
        PlayerTwoSlide.Stop();
        PlayerTwoSlide.GoLastFrame();
        PlayerOneFace.FaceID = -1;
        PlayerTwoFace.FaceID = -1;
        UsePlayerOne = false;
        UsePlayerTwo = false;
        PlayerOneSwitch.IsDown = false;
        PlayerOneSwitch.Name = "";
        PlayerTwoSwitch.IsDown = false;
        PlayerTwoSwitch.Name = "";
        Difficulty.State = 0;
        GameMode.State = 0;
        AiPlayers.State = 0;
    }

    void OnButtonExit()
    {
        app.Close();
    }
}
