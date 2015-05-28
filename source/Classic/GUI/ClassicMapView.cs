
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

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Classic.Logic;

namespace Burntime.Classic.GUI
{
    public class ClassicMapView : MapView
    {
        public ClassicMapView(IMapEntranceHandler Handler, Module App)
            : base(Handler, App)
        {
        }

        public override void OnRender(RenderTarget Target)
        {
            base.OnRender(Target);
            Target.Layer += 2;

            if (Map == null)
                return;

            List<int> renderWays = new List<int>();
            Vector2 offset = (Vector2)ScrollPosition;
            ClassicGame game = (ClassicGame)app.GameState;

            for (int i = 0; i < 4; i++)
            {
                Player p = game.World.Players[i];
                if (p.IsDead)
                    continue;

                if (p.IsTraveling)
                {
                    for (int j = 0; j < p.Location.Neighbors.Count; j++)
                    {
                        if (p.Location.Neighbors[j] == p.Destination)
                            renderWays.Add(p.Location.Ways[j]);
                    }
                }
            }

            int active = game.World.ActivePlayer;
            if (active != -1)
            {
                foreach (int way in Ways.Cross[game.World.ActivePlayerObj.Location].Ways)
                {
                    Player p = game.World.ActivePlayerObj;

                    // get start point and end point
                    Location start = p.Location;
                    Location end = game.World.Locations[Ways.Ways[way].End];
                    if (end == start)
                        end = game.World.Locations[Ways.Ways[way].Start];
                    
                    // check travel allowance
                    if (p.CanTravel(start, end))
                        renderWays.Add(way);
                }
            }

            foreach (int way in renderWays)
            {
                if (Ways.Ways[way].Images.Length > 0)
                {
                    Vector2 pos = Ways.Ways[way].Position + offset;

                    foreach (Sprite sprite in Ways.Ways[way].Images)
                    {
                        Target.DrawSprite(pos, sprite);
                        pos.x += 32;
                    }
                }
            }

        }
    }
}
