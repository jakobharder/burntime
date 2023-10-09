using System;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Classic.Scenes
{
    class ChurchScene : Scene
    {
        GuiFont font;
        int txtoffset;
        float txtline;
        int txtlines;

        public ChurchScene(Module app)
            : base(app)
        {
            Music = "14_MUS 14_HSC.ogg";
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            Image ani = new Image(app);
            ani.Position = new Vector2(200, 117);
            ani.Background = "film_08.ani??p";
            ani.Background.Animation.Speed = 4.5f;
            ani.Background.Animation.IntervalMargin = 1.0f;
            ani.Background.Animation.Progressive = false;
            Windows += ani;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(72, 72, 76));

            CaptureAllMouseClicks = true;
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        protected override void OnActivateScene(object parameter)
        {
            BurntimeClassic game = app as BurntimeClassic;
            Background = "film_08.pac";
            app.RenderMouse = false;

            txtlines = 9;
            txtline = 0;
            txtoffset = 590;
        }

        public override bool OnMouseClick(Vector2 position, MouseButton button)
        {
            app.SceneManager.PreviousScene();

            return base.OnMouseClick(position, button);
        }

        public override bool OnVKeyPress(Keys key)
        {
            app.SceneManager.PreviousScene();

            return true;
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
        }

        public override void OnUpdate(float elapsed)
        {
            base.OnUpdate(elapsed);

            if (txtlines != 0)
            {
                txtline += elapsed * 0.25f;
                if (txtline >= txtlines)
                {
                    txtlines = 0;
                }
            }
        }

        public override void OnRender(RenderTarget target)
        {
            base.OnRender(target);

            if (txtlines != 0)
            {
                TextHelper txt = new TextHelper(app, "burn");
                String line = txt[txtoffset + (int)txtline];
                font.DrawText(target, new Vector2(160, 200 - 15), line, TextAlignment.Center, VerticalTextAlignment.Top);
            }
        }
    }
}
