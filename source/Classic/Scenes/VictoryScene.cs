using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Framework.Network;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Classic.Scenes
{
    class VictoryScene : Scene
    {
        GuiFont font;
        int txtoffset;
        float txtline;
        int txtlines;
        string name;

        public VictoryScene(Module app)
            : base(app)
        {
            Background = "film_01.pac";
            Music = "11_MUS 11_HSC.ogg";
            CaptureAllMouseClicks = true;
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;

            Image ani = new Image(app);
            ani.Background = "film_01.ani?0-21?p";
            ani.Position = new Vector2(170, 43);
            Windows += ani;
            ani = new Image(app);
            ani.Background = "film_01.ani?22-23?p";
            ani.Position = new Vector2(33, 23);
            Windows += ani;
            ani = new Image(app);
            ani.Background = "film_01.ani?24-27?p";
            ani.Position = new Vector2(126, 121);
            Windows += ani;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(72, 72, 76));
        }

        protected override void OnActivateScene(object parameter)
        {
            BurntimeClassic.Instance.RenderMouse = false;

            name = ((VictoryNews)parameter).Name;

            txtlines = 50;
            txtline = 0;
            txtoffset = 610;
        }

        public override void OnUpdate(float elapsed)
        {
            base.OnUpdate(elapsed);

            if (txtlines != 0)
            {
                txtline += elapsed * 0.2f;
                if (txtline >= txtlines)
                {
                    txtlines = 0;
                }
            }
        }

        public override void OnRender(IRenderTarget target)
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
