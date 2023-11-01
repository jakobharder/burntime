using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.GUI;

namespace Burntime.Remaster.Scenes
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
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
            app.RenderMouse = false;
            handled = false;
            CaptureAllMouseClicks = true;

            Windows.Remove(ani1);
            Windows.Remove(ani2);
            Windows.Remove(ani3);

            if (game.ImageScene == "film_06.pac")
            {
                Music = "pub";

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
                Music = "sounds/trader.ogg";

                ani1 = new Image(app);
                ani1.Background = "film_05.ani?0-17?p";
                ani1.Position = app.IsNewGfx ? new Vector2(76, 120) : new Vector2(98, 120);
                ani1.Background.Animation.Speed = 6.5f;
                ani1.Background.Animation.IntervalMargin = 4;
                ani1.Background.Animation.Progressive = false;
                Windows += ani1;

                ani2 = new Image(app);
                ani2.Background = "film_05.ani?18-19";
                ani2.Position = app.IsNewGfx ? new Vector2(52, 88) : new Vector2(77, 89);
                ani2.Background.Animation.Speed = 6.5f;
                ani2.Background.Animation.IntervalMargin = 5;
                ani2.Background.Animation.ReverseAnimation = true;
                ani2.Background.Animation.Progressive = false;
                Windows += ani2;

                ani3 = new Image(app);
                ani3.Background = "film_05.ani?20-21";
                ani3.Position = app.IsNewGfx ? new Vector2(84, 48) : new Vector2(106, 59);
                ani3.Background.Animation.Speed = 6.5f;
                ani3.Background.Animation.Progressive = false;
                Windows += ani3;
            }
            else if (game.ImageScene == "film_10.pac")
            {
                Music = "sounds/trader.ogg";

                ani1 = new Image(app);
                ani1.Background = "film_10.ani";
                ani1.Position = app.IsNewGfx ? new Vector2(108, 89) : new Vector2(125, 92);
                ani1.Background.Animation.IntervalMargin = 3;
                ani1.Background.Animation.Speed = app.IsNewGfx ? 9.0f : 5.0f;
                Windows += ani1;
            }
            else if (game.ImageScene == "film_02.pac")
            {
                Music = "ruin";
            }
            else if (game.ImageScene == "film_03.pac")
            {
                Music = "death";
            }
            else if (game.ImageScene == "film_04.pac")
            {
                Music = "building";
            }
            else if (game.ImageScene == "film_09.pac")
            {
                Music = "death";
            }
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            if (BurntimeClassic.Instance.ImageScene == "film_10.pac" && ani1 is not null)
            {
                ani1.Position = app.IsNewGfx ? new Vector2(108, 89) : new Vector2(125, 92);
                ani1.Background.Animation.Speed = app.IsNewGfx ? 9.0f : 5.0f;
            }
            else if (BurntimeClassic.Instance.ImageScene == "film_05.pac" && ani1 is not null && ani2 is not null && ani3 is not null)
            {
                ani1.Position = app.IsNewGfx ? new Vector2(76, 120) : new Vector2(98, 120);
                ani2.Position = app.IsNewGfx ? new Vector2(52, 88) : new Vector2(77, 89);
                ani3.Position = app.IsNewGfx ? new Vector2(84, 48) : new Vector2(106, 59);
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

        public override bool OnVKeyPress(SystemKey key)
        {
            if (key == SystemKey.F8 || key == SystemKey.F9)
                return false;

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

        public override void OnRender(RenderTarget target)
        {
            base.OnRender(target);

            target.Layer = app.Engine.MaxLayers - 1;

            const int MARGIN = 32;

            target.RenderRect(-Position,
                new Vector2(app.Engine.Resolution.Game.x, MARGIN), new PixelColor(0, 0, 0));
            target.RenderRect(new Vector2(-Position.x, app.Engine.Resolution.Game.y - MARGIN - Position.y), 
                new Vector2(app.Engine.Resolution.Game.x, MARGIN + 1), new PixelColor(0, 0, 0));

            target.RenderRect(new Vector2(-Position.x, -Position.y + MARGIN),
                new Vector2(MARGIN, app.Engine.Resolution.Game.y - MARGIN * 2), new PixelColor(0, 0, 0));
            target.RenderRect(new Vector2(-Position.x + app.Engine.Resolution.Game.x - MARGIN, -Position.y + MARGIN), 
                new Vector2(MARGIN + 1, app.Engine.Resolution.Game.y - MARGIN * 2), new PixelColor(0, 0, 0));
        }
    }
}
