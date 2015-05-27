/*
 *  Burntime Classic
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
 *  contact: 
 *    juern - burntimedeluxe@gmail.com or yn.harada@gmail.com
 * 
 *  authors: 
 *    Juernjakob Harder - 原田ゆあん (yn.harada@gmail.com)
 * 
*/

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
            {
                mapState.Hover = new MapViewHoverInfo(obj, app.ResourceManager, new PixelColor(180, 152, 112));
                Vector2 t = mapState.Hover.Position;
                t.y += 8;
                mapState.Hover.Position = t;
            }
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
                Vector2 pos = item.Position - position;

                if (pos.Length < 10)
                    return item;
            }

            return null;
        }
    }
}
