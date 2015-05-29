
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using Burntime.Framework;
using Burntime.Framework.GUI;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Classic.GUI
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
            target.RenderRect(Vector2.Zero, new Vector2((int)System.Math.Ceiling(Progress * (Size.x - border * 2)) + border, Size.y), color);
            target.Layer -= 1;

            target.Layer += 3;
            font.DrawText(target, new Vector2(Size.x / 2, Size.y / 2), Text, TextAlignment.Center, VerticalTextAlignment.Center);
            target.Layer -= 3;
        }
    }
}
