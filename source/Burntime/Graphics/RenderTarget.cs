using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

using SlimDX.Direct3D9;

namespace Burntime.Platform.Graphics
{
    public class RenderTarget : RenderTarget
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
        public void DrawSprite(SlimDx.Graphics.Sprite sprite)
        {
            if (sprite == null)
                return;

            DrawSprite(Vector2.Zero, sprite);
        }

        public void DrawSprite(Vector2 pos, SlimDx.Graphics.Sprite sprite)
        {
            if (sprite == null)
                return;

            engine.RenderSprite(sprite, pos + rc.Position + offset);
        }

        public void DrawSprite(Vector2 pos, SlimDx.Graphics.Sprite sprite, float alpha)
        {
            if (sprite == null)
                return;

            engine.RenderSprite(sprite, pos + rc.Position + offset, alpha);
        }

        public void DrawSprite(Vector2 pos, SlimDx.Graphics.Sprite sprite, Rect srcRect)
        {
            if (sprite == null)
                return;

            engine.RenderSprite(sprite, pos + rc.Position + offset);
        }

        SlimDx.Graphics.Sprite selImage;
        public void SelectSprite(SlimDx.Graphics.Sprite sprite)
        {
            selImage = sprite;
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

        void RenderTarget.SelectSprite(ISprite sprite) => SelectSprite(sprite as SlimDx.Graphics.Sprite);
        void RenderTarget.DrawSprite(ISprite sprite) => DrawSprite(sprite as SlimDx.Graphics.Sprite);
        void RenderTarget.DrawSprite(Vector2 pos, ISprite sprite, float alpha) => DrawSprite(pos, sprite as SlimDx.Graphics.Sprite, alpha);
        void RenderTarget.DrawSprite(Vector2 pos, ISprite sprite, Rect srcRect) => DrawSprite(pos, sprite as SlimDx.Graphics.Sprite, srcRect);
        RenderTarget RenderTarget.GetSubBuffer(Rect rc) => GetSubBuffer(rc);
    }
}
