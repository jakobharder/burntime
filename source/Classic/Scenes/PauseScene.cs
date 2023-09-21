using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Framework.Network;

namespace Burntime.Classic.Scenes
{
    class PauseScene : Scene
    {
        GuiFont font;
        bool handled = false;

        public PauseScene(Module App)
            : base(App)
        {
            Size = new Burntime.Platform.Vector2(320, 200);
            Position = (app.Engine.GameResolution - new Vector2(320, 200)) / 2;

            //Background = new SpriteImage(App, "blz.pac");
            font = new GuiFont(BurntimeClassic.FontName, new PixelColor(255, 255, 255));
            CaptureAllMouseClicks = true;
        }

        public override void OnRender(RenderTarget Target)
        {
            font.DrawText(Target, new Vector2(160, 100), new GuiString("@newburn?11"), TextAlignment.Center, VerticalTextAlignment.Center);
        }

        public override bool OnMouseClick(Vector2 Position, MouseButton Button)
        {
            if (!handled)
            {
                app.SceneManager.PreviousScene();
                handled = true;
            }
            return true;
        }

        public override bool OnKeyPress(char Key)
        {
            if (!handled)
            {
                app.SceneManager.PreviousScene();
                handled = true;
            }
            return true;
        }

        public override bool OnVKeyPress(Keys Key)
        {
            if (!handled)
            {
                app.SceneManager.PreviousScene();
                handled = true;
            }
            return true;
        }

        protected override void OnActivateScene(object parameter)
        {
            app.RenderMouse = false;
            handled = false;
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
        }
    }
}
