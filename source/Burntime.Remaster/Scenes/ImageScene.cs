using System;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.IO;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Remaster.GUI;

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
        GuiImage originalImage;
        GuiImage fittedImage;
        Vector2 fittedScreen;
        string[][] fittedSubtitles = Array.Empty<string[]>();
        int subtitleBandHeight;
        int subtitleTotalCount;
        bool UseSmallLayout => app.SmallDeviceUi;

        public ImageScene(Module App)
            : base(App)
        {
            subtitleFont = new GuiFont(BurntimeClassic.FontName, ClassicColors.LightGray);
        }

        protected override void OnActivateScene(object parameter)
        {
            request = parameter as ImageSceneRequest ?? throw new ArgumentException(
                "ImageScene requires an ImageSceneRequest.", nameof(parameter));
            Background = originalImage = request.Resource;
            fittedImage = null;
            fittedSubtitles = Array.Empty<string[]>();
            subtitleBandHeight = 0;
            subtitleTotalCount = 0;
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
                    subtitleCount = subtitleTotalCount = subtitles.GetInt("count");
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

            UpdateLayout();
        }

        void UpdateLayout()
        {
            if (!UseSmallLayout)
            {
                if (originalImage != null)
                    Background = originalImage;
                Size = new Vector2(320, 200);
                Position = (app.Engine.Resolution.Game - Size) / 2;
                return;
            }

            Size = app.Engine.Resolution.SafeArea.Size;
            Position = app.Engine.Resolution.SafeArea.Position;
            if (originalImage == null || !originalImage.IsLoaded ||
                originalImage.Width <= 0 || originalImage.Height <= 0)
                return;
            if (fittedImage != null && fittedScreen == Size)
            {
                Background = fittedImage;
                Size = fittedScreen;
                return;
            }

            fittedSubtitles = new string[subtitleTotalCount][];
            int maximumLines = 0;
            if (subtitleTotalCount > 0)
            {
                TextHelper text = new(app, subtitleText);
                if (!string.IsNullOrEmpty(subtitleArgument))
                    text.AddArgument(subtitleArgument, subtitleArgumentValue);
                for (int i = 0; i < subtitleTotalCount; i++)
                {
                    fittedSubtitles[i] = ManualTextLayout.Wrap(
                        text[subtitleFirst + i].Split('\n'), System.Math.Max(1, Size.x - 12),
                        subtitleFont.GetWidth, subtitleFont.GetWidth);
                    maximumLines = System.Math.Max(maximumLines, fittedSubtitles[i].Length);
                }
            }
            subtitleBandHeight = maximumLines > 0 ? maximumLines * subtitleFont.LineHeight + 8 : 0;
            float scale = System.Math.Min((float)Size.x / originalImage.Width,
                (float)System.Math.Max(1, Size.y - subtitleBandHeight) / originalImage.Height);
            Background = fittedImage = originalImage.CloneScaled(System.Math.Min(1, scale));
            Size = fittedScreen = app.Engine.Resolution.SafeArea.Size;
        }

        protected override Vector2 GetBackgroundPosition(Vector2 backgroundSize) =>
            UseSmallLayout
                ? new Vector2((Size.x - backgroundSize.x) / 2,
                    (Size.y - subtitleBandHeight - backgroundSize.y) / 2)
                : base.GetBackgroundPosition(backgroundSize);

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
            if (UseSmallLayout)
                UpdateLayout();
            base.OnRender(target);

            if (drawFrame && !UseSmallLayout)
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
                if (UseSmallLayout)
                {
                    int index = (int)subtitleLine;
                    if (index < fittedSubtitles.Length)
                    {
                        target.RenderRect(new Vector2(0, Size.y - subtitleBandHeight),
                            new Vector2(Size.x, subtitleBandHeight), PixelColor.Black);
                        int y = Size.y - subtitleBandHeight + 4;
                        foreach (string line in fittedSubtitles[index])
                        {
                            subtitleFont.DrawText(target, new Vector2(Size.x / 2, y), line,
                                TextAlignment.Center, VerticalTextAlignment.Top);
                            y += subtitleFont.LineHeight;
                        }
                    }
                    return;
                }
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
