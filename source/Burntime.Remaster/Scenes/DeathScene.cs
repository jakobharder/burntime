using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster.Scenes
{
    class DeathScene : Scene
    {
        GuiFont font;
        int txtoffset;
        float txtline;
        int txtlines;
        string name;

        Image bird;

        public DeathScene(Module app)
            : base(app)
        {
            Background = "film_00.pac";
            Music = "09_MUS 09_HSC.ogg";
            CaptureAllMouseClicks = true;
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            Image ani = new Image(app);
            ani.Background = "film_00.ani?0-10?p";
            ani.Position = new Vector2(16, 68);
            Windows += ani;
            ani = new Image(app);
            ani.Background = "film_00.ani?11-21?p";
            ani.Position = new Vector2(176, 86);
            Windows += ani;
            ani = new Image(app);
            ani.Background = "film_00.ani?22-32?p";
            ani.Position = new Vector2(264, 77);
            Windows += ani;

            bird = new Image(app);
            bird.Background = "film_00.ani?33-72?p";
            bird.Position = new Vector2(166, 20);
            bird.Background.Animation.Endless = false;
            bird.Background.Animation.Delay = 8;
            Windows += bird;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(72, 72, 76));
        }

        public override void OnResizeScreen()
        {
            base.OnResizeScreen();

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        protected override void OnActivateScene(object parameter)
        {
            BurntimeClassic game = app as BurntimeClassic;
            app.RenderMouse = false;

            name = (string)parameter;

            txtlines = 9;
            txtline = 0;
            txtoffset = 600;

            bird.Background.Animation.GoFirstFrame();
            bird.Background.Animation.Start();
        }

        public override void OnUpdate(float elapsed)
        {
            base.OnUpdate(elapsed);

            if (txtlines != 0)
            {
                txtline += elapsed * 0.05f;
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
                txt.AddArgument("|A", name);
                string line = txt[txtoffset + (int)txtline];
                font.DrawText(target, new Vector2(160, 200 - 15), line, TextAlignment.Center, VerticalTextAlignment.Top);
            }
        }

        public override bool OnMouseClick(Vector2 position, MouseButton button)
        {
            app.ActiveClient.Finish();
            app.SceneManager.PreviousScene();

            return base.OnMouseClick(position, button);
        }

        public override bool OnVKeyPress(Keys key)
        {
            app.ActiveClient.Finish();
            app.SceneManager.PreviousScene();

            return true;
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
        }
    }
}
