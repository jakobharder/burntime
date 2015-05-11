/*
 *  Burntime Platform
 *  Copyright (C) 2009
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
 *  authors: 
 *    Juernjakob Harder (yn.harada@gmail.com)
 * 
*/

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
