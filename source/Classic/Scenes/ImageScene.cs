﻿
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Classic.GUI;

namespace Burntime.Classic.Scenes
{
    class ImageScene : Scene
    {
        Image ani1, ani2, ani3;
        bool handled = false;

        public ImageScene(Module App)
            : base(App)
        {
        }

        protected override void OnActivateScene(object parameter)
        {
            BurntimeClassic game = app as BurntimeClassic;
            Background = game.ImageScene;
            Size = new Vector2(320, 200);
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;
            app.RenderMouse = false;
            handled = false;
            CaptureAllMouseClicks = true;

            Windows.Remove(ani1);
            Windows.Remove(ani2);
            Windows.Remove(ani3);

            if (game.ImageScene == "film_06.pac")
            {
                Music = "19_MUS 19_HSC.ogg";

                ani1 = new Image(app);
                ani1.Background = "film_06.ani?0-5";
                ani1.Position = new Vector2(49, 46);
                ani1.Background.Animation.IntervalMargin = 6;
                ani1.Background.Animation.Speed = 6.5f;
                Windows += ani1;

                ani2 = new Image(app);
                ani2.Background = "film_06.ani?6-15";
                ani2.Position = new Vector2(179, 96);
                ani2.Background.Animation.Speed = 6.5f;
                Windows += ani2;
            }
            else if (game.ImageScene == "film_05.pac")
            {
                Music = "21_MUS 21_HSC.ogg";

                ani1 = new Image(app);
                ani1.Background = "film_05.ani?0-17?p";
                ani1.Position = new Vector2(98, 120);
                ani1.Background.Animation.Speed = 6.5f;
                ani1.Background.Animation.IntervalMargin = 4;
                ani1.Background.Animation.Progressive = false;
                Windows += ani1;

                ani2 = new Image(app);
                ani2.Background = "film_05.ani?18-19";
                ani2.Position = new Vector2(77, 89);
                ani2.Background.Animation.Speed = 6.5f;
                ani2.Background.Animation.IntervalMargin = 5;
                ani2.Background.Animation.ReverseAnimation = true;
                ani2.Background.Animation.Progressive = false;
                Windows += ani2;

                ani3 = new Image(app);
                ani3.Background = "film_05.ani?20-21";
                ani3.Position = new Vector2(106, 59);
                ani3.Background.Animation.Speed = 6.5f;
                ani3.Background.Animation.Progressive = false;
                Windows += ani3;
            }
            else if (game.ImageScene == "film_10.pac")
            {
                Music = "21_MUS 21_HSC.ogg";

                ani1 = new Image(app);
                ani1.Background = "film_10.ani";
                ani1.Position = new Vector2(125, 92);
                ani1.Background.Animation.IntervalMargin = 3;
                ani1.Background.Animation.Speed = 5.0f;
                Windows += ani1;
            }
            else if (game.ImageScene == "film_02.pac")
            {
                Music = "07_MUS 07_HSC.ogg";
            }
            else if (game.ImageScene == "film_03.pac")
            {
                Music = "09_MUS 09_HSC.ogg";
            }
            else if (game.ImageScene == "film_04.pac")
            {
                Music = "05_MUS 05_HSC.ogg";
            }
            else if (game.ImageScene == "film_08.pac")
            {
                Music = "02_MUS 02_HSC.ogg";
            }
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            if (!handled)
            {
                PreviousScene();
                handled = true;
            }
            return true;
        }

        public override bool OnKeyPress(char Key)
        {
            if (!handled)
            {
                PreviousScene();
                handled = true;
            }
            return true;
        }

        public override bool OnVKeyPress(Keys Key)
        {
            if (!handled)
            {
                PreviousScene();
                handled = true;
            }
            return true;
        }

        private void PreviousScene()
        {
            BurntimeClassic game = app as BurntimeClassic;
            if (game.ActionAfterImageScene != ActionAfterImageScene.None)
            {
                switch (game.ActionAfterImageScene)
                {
                    case ActionAfterImageScene.Trader:
                        app.SceneManager.SetScene("TraderScene", true);
                        break;
                    case ActionAfterImageScene.Pub:
                        app.SceneManager.SetScene("PubScene", true);
                        break;
                }
            }
            else
            {
                app.SceneManager.PreviousScene();
            }
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
        }
    }
}
