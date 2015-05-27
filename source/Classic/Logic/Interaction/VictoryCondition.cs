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

using System;
using Burntime.Framework.States;
using Burntime.Platform;

namespace Burntime.Classic.Logic.Interaction
{
    [Serializable]
    public class VictoryCondition : StateObject
    {
        public bool Process(Player player)
        {
            ClassicGame world = (ClassicGame)container.Root;

            // check all locations
            for (int i = 0; i < world.World.Locations.Count; i++)
            {
                Location l = world.World.Locations[i];

                // check for neighbored city
                bool neighborOfCity = false;
                for (int j = 0; j < l.Neighbors.Count; j++)
                {
                    if (l.Neighbors[j].IsCity)
                    {
                        neighborOfCity = true;
                        break;
                    }
                }

                if (neighborOfCity)
                {
                    // if the location is not possesed by the player, then he hasn't won yet
                    if (l.Player != player)
                        return false;
                }
            }

            // all city neighbors conquered, victory
            return true;
        }
    }
}
