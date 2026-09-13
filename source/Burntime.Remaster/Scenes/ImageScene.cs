using System;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.IO;
using Burntime.Framework;

namespace Burntime.Remaster.Scenes
{
    class ImageScene : Scene
    {
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
            Music = null;
            MusicLoop = true;

            if (game.ImageScene.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                ConfigFile config = new();
                config.Open(app.ResourceManager.ResolveFileReplacement(game.ImageScene));
                ConfigSection music = config["music"];
                Music = music.GetString("song");
                MusicLoop = music.GetBool("loop", true);
            }
        }

        public override void OnResizeScreen(bool reload = false)
        {
            base.OnResizeScreen(reload);

            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;

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

        public override bool OnInputAction(InputAction action)
        {
            if (action != InputAction.Primary && action != InputAction.Back)
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
