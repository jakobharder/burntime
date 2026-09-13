using System;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Remaster.Scenes
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
            Music = "church";
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(72, 72, 76));

            CaptureAllMouseClicks = true;
        }

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        protected override void OnActivateScene(object parameter)
        {
            BurntimeClassic game = app as BurntimeClassic;
            Background = "scenes/church.txt";
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

        public override bool OnInputAction(InputAction action)
        {
            if (action != InputAction.Primary && action != InputAction.Back)
                return false;

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
