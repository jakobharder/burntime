using System;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Remaster.GUI
{
    /// <summary>
    /// Face window.
    /// </summary>
    class ProgressWindow : Container
    {
        #region private attributes
        private float progress;
        private string text;
        private PixelColor color;
        private GuiFont font;
        private int border;
        #endregion

        #region public attributes
        /// <summary>
        /// Current progress (0 - 1).
        /// </summary>
        public float Progress
        {
            get { return progress; }
            set { progress = value; }
        }

        /// <summary>
        /// Display text.
        /// </summary>
        public string Text
        {
            get { return text; }
            set { text = value; }
        }

        /// <summary>
        /// Progress bar color.
        /// </summary>
        public PixelColor Color
        {
            get { return color; }
            set { color = value; }
        }

        /// <summary>
        /// Amount of border pixels that should not be used for the progress bar.
        /// </summary>
        public int Border
        {
            get { return border; }
            set { border = value; }
        }
        #endregion

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="app">game module</param>
        public ProgressWindow(Module app, string background, string foreground)
            : base(app)
        {
            this.Background = background;

            Image image = new Image(app, foreground);
            image.Layer += 2;
            Windows += image;

            font = new GuiFont(BurntimeClassic.FontName, PixelColor.White);
        }

        public override void OnRender(Platform.Graphics.RenderTarget target)
        {
            base.OnRender(target);

            target.Layer += 1;
            target.RenderRect(new Vector2(border, border), new Vector2((int)System.Math.Ceiling(Progress * (Size.x - border * 2)), Size.y - border * 2), color);
            target.Layer -= 1;

            target.Layer += 3;
            font.DrawText(target, new Vector2(Size.x / 2, Size.y / 2), Text, TextAlignment.Center, VerticalTextAlignment.Center);
            target.Layer -= 3;
        }
    }
}
