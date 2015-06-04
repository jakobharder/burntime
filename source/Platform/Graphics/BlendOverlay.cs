
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
using System.Threading;

using SlimDX.Direct3D9;

namespace Burntime.Platform.Graphics
{
    class BlendOverlay
    {
        FadingHelper blendFade = new FadingHelper(2.5f);
        Vector2 size;
        Texture emptyTexture;

        bool block = false;
        public bool BlockFadeOut
        {
            get { return block; }
            set { block = value; }
        }

        public float Speed
        {
            get { return blendFade.Speed; }
            set { blendFade.Speed = value; }
        }

        public float Blend
        {
            set
            {
                if (value == 0 && blendFade.State != 1)
                {
                    // block thread until full blend is reached
                    Thread.Sleep((int)((1 - blendFade.State) * 1000 / blendFade.Speed));
                }
                blendFade.FadeTo = value;
            }
            get { return blendFade.FadeTo; }
        }

        public float BlendState
        {
            get { return blendFade.State; }
        }

        public BlendOverlay(Vector2 ScreenSize)
        {
            size = ScreenSize;
        }

        public void Render(GameTime RenderTime, SlimDX.Direct3D9.Sprite SpriteRenderer)
        {
            if (!(block && blendFade.IsFadingOut))
                blendFade.Update(RenderTime.Elapsed);

            if (!blendFade.IsOut)
            {
                SpriteRenderer.Transform = SlimDX.Matrix.Identity;
                System.Drawing.Rectangle rc = new System.Drawing.Rectangle(0, 0, size.x, size.y);
                SpriteRenderer.Draw(emptyTexture, rc, new SlimDX.Vector3(0, 0, 0), new SlimDX.Vector3(0, 0, 0), new SlimDX.Color4(blendFade.State, 0, 0, 0));
            }
        }

        public void Load(Device device)
        {
            emptyTexture = new Texture(device, 1, 1, 1, Usage.None, Format.A8R8G8B8, Pool.Managed);
            SlimDX.DataRectangle dr = emptyTexture.LockRectangle(0, LockFlags.Discard);
            dr.Data.Write<uint>(0xffffffff);
            emptyTexture.UnlockRectangle(0);
        }

        public void Unload()
        {
            emptyTexture.Dispose();
        }

        public void WaitForBlend()
        {
            // block thread until full blend is reached
            Thread.Sleep(500 + (int)((1 - blendFade.State) * 1000 / blendFade.Speed));
        }
    }
}
