
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
    class MapViewOverlayDroppedItems : IMapViewOverlay
    {
        Sprite[] icons;
        Location mapState;
        Module app;

        public bool IsVisible
        {
            get { return true; }
            set { }
        }

        public MapViewOverlayDroppedItems(Module app)
        {
            this.app = app;

            icons = new Sprite[2];
            icons[0] = app.ResourceManager.GetImage("munt.raw?3");
            icons[1] = app.ResourceManager.GetImage("munt.raw?4");
        }

        public void MouseMoveOverlay(Vector2 position)
        {
            if (mapState == null)
                return;

            IMapObject obj = GetObjectAt(position);
            if (obj != null)
                mapState.Hover = new MapViewHoverInfo(obj, app.ResourceManager, new PixelColor(180, 152, 112));
        }

        public void UpdateOverlay(WorldState world, float elapsed)
        {
            mapState = world.CurrentLocation as Location;
        }

        public void RenderOverlay(RenderTarget target, Vector2 offset, Vector2 size)
        {
            if (mapState != null)
            {
                for (int i = 0; i < mapState.Items.Count; i++)
                {
                    DroppedItem item = mapState.Items.MapObjects[i];
                    target.DrawSprite(item.Position + offset - new Vector2(4, 2), icons[item.Icon]);
                }
            }
        }

        public IMapObject GetObjectAt(Vector2 position)
        {
            if (mapState == null)
                return null;

            for (int i = 0; i < mapState.Items.Count; i++)
            {
                DroppedItem item = mapState.Items.MapObjects[i];
                Vector2 distance = item.Position - position;

                if (distance.Length < 10)
                    return item;
            }

            return null;
        }
    }
}
