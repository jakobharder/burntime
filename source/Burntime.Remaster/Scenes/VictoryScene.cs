using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Framework.Network;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster.Scenes
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
            Music = "victory";
            CaptureAllMouseClicks = true;
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(72, 72, 76));
        }

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
        }

        protected override void OnActivateScene(object parameter)
        {
            Background = "scenes/victory.txt";
            BurntimeClassic.Instance.RenderMouse = false;

            name = ((VictoryNews)parameter).Name;

            txtlines = 50;
            txtline = 0;
            txtoffset = 608;
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
            Dismiss();

            return base.OnMouseClick(position, button);
        }

        public override bool OnInputAction(InputAction action)
        {
            if (action != InputAction.Primary && action != InputAction.Back)
                return false;

            Dismiss();
            return true;
        }

        void Dismiss()
        {
            app.ActiveClient.Finish();
            app.SceneManager.PreviousScene();
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
        }
    }
}
