
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

using Burntime.Classic.Logic;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Graphics;

namespace Burntime.Classic.Maps
{
    class MapViewOverlayFlags : IMapViewOverlay
    {
        ClassicGame state;
        Module app;
        SpriteAnimation ani;

        public bool IsVisible
        {
            get { return true; }
            set { }
        }

        public MapViewOverlayFlags(Module app)
        {
            this.app = app;
            ani = new SpriteAnimation(4);
        }

        public void MouseMoveOverlay(Vector2 position)
        {
        }

        public void UpdateOverlay(WorldState world, float elapsed)
        {
            state = world as ClassicGame;
            ani.Update(elapsed);
        }

        public void RenderOverlay(RenderTarget target, Vector2 offset, Vector2 size)
        {
            if (state != null)
            {
                for (int i = 0; i < state.World.Locations.Count; i++)
                {
                    Location l = state.World.Locations[i];

                    if (l.Player != null)
                    {
                        Rect area = state.World.Map.Entrances[i].Area;
                        l.Player.Flag.Object.Animation.Frame = ani.Frame;
                        target.DrawSprite(new Vector2(area.Right, area.Top) + offset, l.Player.Flag);
                    }
                }
            }
        }

        public IMapObject GetObjectAt(Vector2 position)
        {
            return null;
        }
    }
}
