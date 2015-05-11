
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2011 Jakob Harder
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
*/
#endregion

using System;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Classic.Logic.Generation;

namespace Burntime.Classic
{
    public class MenuScene : Scene
    {
        SpriteAnimation PlayerOneSlide;
        SpriteAnimation PlayerTwoSlide;
        NameWindow PlayerOneSwitch;
        NameWindow PlayerTwoSwitch;
        bool UsePlayerOne = false;
        bool UsePlayerTwo = false;
        FaceWindow PlayerOneFace;
        FaceWindow PlayerTwoFace;
        Radio Difficulty;
        Radio Color;
        Burntime.Platform.IO.ConfigFile conversionTable;

        public MenuScene(Module App) 
            : base(App)
        {
            Background = "sta.pac";
            Music = "15_MUS 15_HSC.ogg";
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;

            // face
            PlayerOneFace = new FaceWindow(App);
            PlayerOneFace.MaxFaceID = 5;
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

            // player names
            PlayerOneSwitch = new NameWindow(App);
            PlayerOneSwitch.Position = new Vector2(15, 92);
            PlayerOneSwitch.Image = "sta.ani?6";
            PlayerOneSwitch.DownImage = "sta.ani?7";
            PlayerOneSwitch.DownCommand += OnPlayerOneDown;
            PlayerOneSwitch.UpCommand += OnPlayerOneUp;
            PlayerOneSwitch.Command += OnPlayerOneClick;
            PlayerOneSwitch.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(184, 184, 184));
            Windows += PlayerOneSwitch;
            PlayerTwoSwitch = new NameWindow(App);
            PlayerTwoSwitch.Position = new Vector2(204, 92);
            PlayerTwoSwitch.Image = "sta.ani?6";
            PlayerTwoSwitch.DownImage = "sta.ani?7";
            PlayerTwoSwitch.DownCommand += OnPlayerTwoDown;
            PlayerTwoSwitch.UpCommand += OnPlayerTwoUp;
            PlayerTwoSwitch.Command += OnPlayerTwoClick;
            PlayerTwoSwitch.Font = new GuiFont(BurntimeClassic.FontName, new PixelColor(184, 184, 184));
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
            for (int i = 0; i < 3; i++)
            {
                radio = new Radio(App);
                if (i == 0)
                {
                    radio.IsDown = true;
                    Difficulty = radio;
                }
                radio.Position = new Vector2(100 + 45 * i, 149);
                radio.Image = "sta.ani?" + (i * 2).ToString();
                radio.HoverImage = "sta.ani?" + (i * 2 + 1).ToString();
                radio.DownImage = "sta.ani?" + (i * 2 + 1).ToString();
                radio.Group = 1;
                Windows += radio;
            }

            // input conversion
            conversionTable = new Burntime.Platform.IO.ConfigFile();
            conversionTable.Open(Burntime.Platform.IO.FileSystem.GetFile("conversion_table.txt"));
            PlayerOneSwitch.Table = conversionTable;
            PlayerTwoSwitch.Table = conversionTable;
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

        void OnButtonStart()
        {
            if (PlayerOneSwitch.Name == "" && PlayerTwoSwitch.Name == "")
                return;

            app.Engine.Blend = 1;

            GameCreation creation = new GameCreation(app as BurntimeClassic);

            NewGameInfo Info = new NewGameInfo();
            Info.NameOne = PlayerOneSwitch.Name;
            Info.NameTwo = PlayerTwoSwitch.Name;
            Info.FaceOne = PlayerOneFace.FaceID;
            Info.FaceTwo = PlayerTwoFace.FaceID;
            Info.Difficulty = Difficulty.Value;
            Info.ColorOne = Color.IsDown ? BurntimePlayerColor.Red : BurntimePlayerColor.Green;
            Info.ColorTwo = Color.IsDown ? BurntimePlayerColor.Green : BurntimePlayerColor.Red;

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
            Difficulty.IsDown = true;
        }
    }
}
