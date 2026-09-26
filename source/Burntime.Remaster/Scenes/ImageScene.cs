using System;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.IO;
using Burntime.Framework;
using Burntime.Framework.GUI;

namespace Burntime.Remaster.Scenes
{
    class ImageScene : Scene
    {
        bool handled = false;
        readonly GuiFont subtitleFont;
        string subtitleText = null;
        int subtitleFirst;
        int subtitleCount;
        float subtitleLine;
        float subtitleSpeed;
        Vector2 subtitlePosition;
        string subtitleArgument = null;
        string subtitleArgumentValue = null;
        bool drawFrame;
        ImageSceneRequest request;

        public ImageScene(Module App)
            : base(App)
        {
            subtitleFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.LightGray);
        }

        protected override void OnActivateScene(object parameter)
        {
            request = parameter as ImageSceneRequest ?? throw new ArgumentException(
                "ImageScene requires an ImageSceneRequest.", nameof(parameter));
            Background = request.Resource;
            Size = new Vector2(320, 200);
            Position = (app.Engine.Resolution.Game - new Vector2(320, 200)) / 2;
            app.RenderMouse = false;
            handled = false;
            CaptureAllMouseClicks = true;
            Music = null;
            MusicLoop = true;
            subtitleText = null;
            subtitleCount = 0;
            subtitleArgument = null;
            subtitleArgumentValue = request.SubtitleArgument;
            drawFrame = false;

            if (request.Resource.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                ConfigFile config = new();
                config.Open(app.ResourceManager.ResolveFileReplacement(request.Resource));
                drawFrame = config["image"].GetBool("frame");
                ConfigSection music = config["music"];
                Music = music.GetString("song");
                MusicLoop = music.GetBool("loop", true);

                ConfigSection subtitles = config["subtitles"];
                subtitleText = subtitles.GetString("text");
                if (!string.IsNullOrEmpty(subtitleText))
                {
                    subtitleFirst = subtitles.GetInt("first");
                    subtitleCount = subtitles.GetInt("count");
                    subtitleSpeed = subtitles.GetFloat("speed");
                    subtitlePosition = subtitles.GetVector2("position");
                    subtitleArgument = subtitles.GetString("argument");
                    subtitleLine = 0;
                }
            }
        }

        public override void OnUpdate(float elapsed)
        {
            base.OnUpdate(elapsed);

            if (subtitleCount > 0)
            {
                subtitleLine += elapsed * subtitleSpeed;
                if (subtitleLine >= subtitleCount)
                    subtitleCount = 0;
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
            if (request.FinishClient)
                app.ActiveClient.Finish();

            if (!string.IsNullOrEmpty(request.NextScene))
                app.SceneManager.SetScene(request.NextScene, true,
                    request.NextSceneParameter);
            else
                app.SceneManager.PreviousScene();
        }

        protected override void OnInactivateScene()
        {
            app.RenderMouse = true;
        }

        public override void OnRender(RenderTarget target)
        {
            base.OnRender(target);

            if (drawFrame)
            {
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

            if (subtitleCount > 0)
            {
                target.Layer = app.Engine.MaxLayers;
                TextHelper text = new(app, subtitleText);
                if (!string.IsNullOrEmpty(subtitleArgument))
                    text.AddArgument(subtitleArgument, subtitleArgumentValue);
                subtitleFont.DrawText(target, subtitlePosition,
                    text[subtitleFirst + (int)subtitleLine], TextAlignment.Center,
                    VerticalTextAlignment.Top);
            }
        }
    }
}
