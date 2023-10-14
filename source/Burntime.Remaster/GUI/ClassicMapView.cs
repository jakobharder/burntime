using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Framework;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.GUI
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

                    foreach (ISprite sprite in Ways.Ways[way].Images)
                    {
                        Target.DrawSprite(pos, sprite);
                        pos.x += 32;
                    }
                }
            }

        }
    }
}
