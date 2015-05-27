
#region GNU General Public License - Burntime
/*
 *  Burntime
 *  Copyright (C) 2008-2013 Jakob Harder
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
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
