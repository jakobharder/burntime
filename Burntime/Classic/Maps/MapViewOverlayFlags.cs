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
