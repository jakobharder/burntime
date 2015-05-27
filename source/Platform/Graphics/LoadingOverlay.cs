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

using SlimDX.Direct3D9;
using Burntime.Platform.IO;

namespace Burntime.Platform.Graphics
{
    class LoadingOverlay
    {
        float rotationSpeed = 1.5f;
        float rotationState;
        float fadeOutSpeed = 4;
        float fadeOutState = 1;
        float loadingDelay = 1;
        float loadingDelayState = 0;

        Texture loadingTexture;
        Vector2 size;
        Engine engine;

        public LoadingOverlay(Engine Engine, Vector2 ScreenSize)
        {
            size = ScreenSize;
            engine = Engine;
        }

        public void Render(GameTime RenderTime, SlimDX.Direct3D9.Sprite SpriteRenderer)
        {
            // loading render
            rotationState += rotationSpeed * RenderTime.Elapsed;
            if (rotationState >= 1)
                rotationState -= (float)System.Math.Floor(rotationState);

            if (!engine.isLoading)
            {
                if (engine.loadingStack > 0 || engine.ResourceManager.IsLoading)
                {
                    loadingDelayState += loadingDelay * RenderTime.Elapsed;
                    if (loadingDelayState >= 1)
                    {
                        engine.isLoading = true;
                    }
                }
            }
            else
            {
                if (engine.loadingStack == 0 && !engine.ResourceManager.IsLoading)
                    engine.isLoading = false;
                loadingDelayState = 0;
                fadeOutState = 1;
            }

            if (fadeOutState > 0)
            {
                SpriteRenderer.Begin(SpriteFlags.SortTexture | SpriteFlags.SortDepthFrontToBack | SpriteFlags.AlphaBlend);

                SpriteRenderer.Transform = SlimDX.Matrix.Translation(size.x - 32, size.y - 32, 0);
                SpriteRenderer.Transform = SlimDX.Matrix.RotationZ(-(float)System.Math.PI * rotationState * 2) * SpriteRenderer.Transform;
                SpriteRenderer.Transform = SlimDX.Matrix.Scaling(1.5f, 1.5f, 1.5f) * SpriteRenderer.Transform;

                SpriteRenderer.Draw(loadingTexture, new SlimDX.Vector3(16, 16, 0), new SlimDX.Vector3(0, 0, 0), new SlimDX.Color4(fadeOutState, 1, 1, 1));

                SpriteRenderer.End();

                fadeOutState -= RenderTime.Elapsed * fadeOutSpeed;
            }
        }

        public void Load(Device device)
        {
            loadingTexture = Texture.FromStream(device, FileSystem.GetFile("loading.png"), Usage.None, Pool.Default);
        }

        public void Unload()
        {
            loadingTexture.Dispose();
        }
    }
}
