
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
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

using SlimDX.Direct3D9;

namespace Burntime.Platform.Graphics
{
    public class RenderTarget
    {
        Rect rc;
        Engine engine;
        internal float elapsed;
        Vector2 offset;

        public float Elapsed
        {
            get { return elapsed; }
        }

        public int Width
        {
            get { return rc.Width; }
        }

        public int Height
        {
            get { return rc.Height; }
        }

        public Vector2 ScreenOffset
        {
            get { return rc.Position; }
        }

        public Vector2 ScreenSize
        {
            get { return engine.MainTarget.Size; }
        }

        public Vector2 Size
        {
            get { return rc.Size; }
        }

        public int Layer
        {
            get { return engine.Layer; }
            set { engine.Layer = value; }
        }

        public float Blend
        {
            set { engine.Device.Blend = value; }
            get { return engine.Device.Blend; }
        }
        internal RenderTarget(Engine engine, Rect rc)
        {
            this.engine = engine;
            this.rc = rc;
        }

        public Vector2 Offset
        {
            get { return offset; }
            set { offset = value; }
        }

        #region DrawSprite methods
        public void DrawSprite(Sprite sprite)
        {
            if (sprite == null)
                return;

            DrawSprite(Vector2.Zero, sprite);
        }

        public void DrawSprite(Vector2 pos, Sprite sprite)
        {
            if (sprite == null)
                return;

            engine.RenderSprite(sprite, pos + rc.Position + offset);
        }

        public void DrawSprite(Vector2 pos, Sprite sprite, float alpha)
        {
            if (sprite == null)
                return;

            engine.RenderSprite(sprite, pos + rc.Position + offset, alpha);
        }

        public void DrawSprite(Vector2 pos, Sprite sprite, Rect srcRect)
        {
            if (sprite == null)
                return;

            engine.RenderSprite(sprite, pos + rc.Position + offset);
        }

        Sprite selImage;
        public void SelectSprite(Sprite sprite)
        {
            selImage = sprite;
        }

        public void DrawSelectedSprite(Vector2 pos, Rect srcRect)
        {
            engine.RenderSprite(selImage, pos + rc.Position + offset, srcRect.Position, srcRect.Width, srcRect.Height);
        }

        public void DrawSelectedSprite(Vector2 pos, Rect srcRect, PixelColor color)
        {
            engine.RenderSprite(selImage, pos + rc.Position + offset, srcRect.Position, srcRect.Width, srcRect.Height, color.ToInt());
        }
        #endregion

        public void RenderRect(Vector2 pos, Vector2 size, PixelColor color)
        {
            engine.RenderRect(pos + rc.Position + offset, size, color.ToInt());
        }

        public void RenderFrame(Vector2 pos, Vector2 size, PixelColor color)
        {
            engine.RenderFrame(pos + rc.Position + offset, size, color.ToInt());
        }

        public void RenderLine(Vector2 start, Vector2 end, PixelColor color)
        {
            engine.RenderLine(start + rc.Position + offset, end + rc.Position + offset, color.ToInt());
        }

        public RenderTarget GetSubBuffer(Rect rc)
        {
            RenderTarget target = new RenderTarget(engine, new Rect(this.rc.Position + rc.Position, rc.Size));
            target.elapsed = elapsed;
            target.offset = offset;
            return target;
        }
    }
}
