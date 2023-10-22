using System;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster
{
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
        GuiFont copyright;

        public MenuScene(Module App) 
            : base(App)
        {
            Background = "sta.pac";
            Music = "15_MUS 15_HSC.ogg";
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            copyright = new GuiFont(BurntimeClassic.FontName, new PixelColor(164, 164, 164)) { Borders = TextBorders.Screen };

            // face
            PlayerOneFace = new FaceWindow(App);
            PlayerOneFace.MaxFaceID = MAX_FACE_ID;
            PlayerOneFace.Position = new Vector2(33, 28);
            PlayerOneFace.Group = 3;
            Windows += PlayerOneFace;
            PlayerTwoFace = new FaceWindow(App);
            PlayerTwoFace.MaxFaceID = 5;
            PlayerTwoFace.Position = new Vector2(223, 28);
            PlayerTwoFace.Group = 3;
            Windows += PlayerTwoFace;

            // face slides
            Image image = new Image(App, "sta.ani?10-16");
            image.Position = new Vector2(30, 26);
            image.Layer++;
            PlayerOneSlide = image.Background.Animation;
            Windows += image;
            image = new Image(App, "sta.ani?10-16");
            image.Position = new Vector2(220, 26);
            image.Layer++;
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

            // buttons
            Button button = new Button(App);
            button.Position = new Vector2(131, 42);
            button.Image = "sta.ani?17";
            button.HoverImage = "sta.ani?18";
            button.Command += OnButtonStart;
            Windows += button;
            button = new Button(App);
            button.Position = new Vector2(131, 15);
            button.Image = "sta.ani?19";
            button.HoverImage = "sta.ani?20";
            button.Command += OnButtonLoad;
            Windows += button;

            // exit button
            button = new Button(App);
            button.Image = "gfx/menu_exit.png";
            button.HoverImage = "gfx/menu_exit_hover.png";
            button.Position = new Vector2(276, 163);
            button.Command += OnButtonExit;
            Windows += button;

            // player names
            PlayerOneSwitch = new NameWindow(App);
            PlayerOneSwitch.Position = new Vector2(15, 92);
            PlayerOneSwitch.Image = "sta.ani?6";
            PlayerOneSwitch.DownImage = "sta.ani?7";
            PlayerOneSwitch.DownCommand += OnPlayerOneDown;
            PlayerOneSwitch.UpCommand += OnPlayerOneUp;
            PlayerOneSwitch.Command += OnPlayerOneClick;
            PlayerOneSwitch.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(184, 184, 184));
            PlayerOneSwitch.TextHorizontalAlign = TextAlignment.Center;
            PlayerOneSwitch.TextVerticalAlign = VerticalTextAlignment.Center;
            Windows += PlayerOneSwitch;
            PlayerTwoSwitch = new NameWindow(App);
            PlayerTwoSwitch.Position = new Vector2(204, 92);
            PlayerTwoSwitch.Image = "sta.ani?6";
            PlayerTwoSwitch.DownImage = "sta.ani?7";
            PlayerTwoSwitch.DownCommand += OnPlayerTwoDown;
            PlayerTwoSwitch.UpCommand += OnPlayerTwoUp;
            PlayerTwoSwitch.Command += OnPlayerTwoClick;
            PlayerTwoSwitch.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(184, 184, 184));
            PlayerTwoSwitch.TextHorizontalAlign = TextAlignment.Center;
            PlayerTwoSwitch.TextVerticalAlign = VerticalTextAlignment.Center;
            Windows += PlayerTwoSwitch;

            // color
            Radio radio = new Radio(App);
            radio.Position = new Vector2(45, 121);
            radio.Image = "sta.ani?8";
            radio.DownImage = "sta.ani?9";
            radio.Mode = RadioMode.Round;
            radio.Group = 2;
            Color = radio;
            Windows += radio;
            radio = new Radio(App);
            radio.IsDown = true;
            radio.Position = new Vector2(237, 121);
            radio.Image = "sta.ani?8";
            radio.DownImage = "sta.ani?9";
            radio.Mode = RadioMode.Round;
            radio.Group = 2;
            Windows += radio;

            // difficulty
            Difficulty = new(App);
            Difficulty.Position = new(100, 149);
            Difficulty.ToolTipFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(212, 212, 212)) { Borders = TextBorders.Screen };
            Difficulty.AddState(null, "gfx/ui/start_button_level1.png", "gfx/ui/start_button_level1_down.png", "gfx/ui/start_button_level1_down.png", "@newburn?14");
            Difficulty.AddState(null, "gfx/ui/start_button_level2.png", "gfx/ui/start_button_level2_down.png", "gfx/ui/start_button_level2_down.png", "@newburn?15");
            Difficulty.AddState(null, "gfx/ui/start_button_level3.png", "gfx/ui/start_button_level3_down.png", "gfx/ui/start_button_level3_down.png", "@newburn?16");
            Windows += Difficulty;

            // mode
            GameMode = new(App);
            GameMode.Position = new(145, 149);
            GameMode.ToolTipFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(212, 212, 212)) { Borders = TextBorders.Screen };
            GameMode.AddState(null, "gfx/ui/start_button_remake.png", "gfx/ui/start_button_remake_down.png", "gfx/ui/start_button_remake_down.png", "@newburn?1");
            GameMode.AddState(null, "gfx/ui/start_button_original.png", "gfx/ui/start_button_original_down.png", "gfx/ui/start_button_original_down.png", "@newburn?0");
            Windows += GameMode;

            // ai
            AiPlayers = new(App);
            AiPlayers.Position = new(190, 149);
            AiPlayers.ToolTipFont = new GuiFont(BurntimeClassic.FontName, new PixelColor(212, 212, 212)) { Borders = TextBorders.Screen };
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

        public override void OnRender(RenderTarget Target)
        {
            base.OnRender(Target);

            //copyright.DrawText(Target, new Vector2(Size.x - 2, Size.y + 2), "Remade by Jakob", TextAlignment.Right, VerticalTextAlignment.Top);
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
}
