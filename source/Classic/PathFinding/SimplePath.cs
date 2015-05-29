
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
using Burntime.Data.BurnGfx;
using Burntime.Platform;

namespace Burntime.Classic.PathFinding
{
    [Serializable]
    public class SimplePath : PathState
    {
        protected Vector2f moveTo;
        protected Vector2f position;

        public override Vector2 MoveTo
        {
            get { return moveTo; }
            set { moveTo = value; }
        }

        public override Vector2 Process(PathMask mask, Vector2 position, float elapsed)
        {
            this.position = position;
            Vector2f dif = moveTo - this.position;

            float speedElapsed = elapsed * speed;

            // quit if goal is reached
            if (dif.Length < 0.1f)
                return position;

            // calculate distance
            if (dif.Length > speedElapsed)
            {
                dif.Normalize();
                dif *= speedElapsed;
            }

            // calculate new position
            Vector2f newpos = this.position + dif;
            Rectf boundaries = new Rectf(0, 0, mask.Width * mask.Resolution, mask.Height * mask.Resolution);
            if (boundaries.PointInside(newpos))
            {
                // transform to map mask position
                Vector2 maskpos = mask.ConvertMapToMask(newpos);

                // set new position if there is no obstacle
                if (mask[maskpos])
                    this.position = newpos;
                else // otherwise just cancel walking
                    moveTo = this.position;
            }
            else // otherwise just cancel walking
                moveTo = this.position;

            return this.position;
        }

        public override void DebugRender(Burntime.Platform.Graphics.RenderTarget target)
        {
            if (position != moveTo)
                target.RenderLine(position, moveTo, new PixelColor(255, 0, 0));
        }
    }
}
