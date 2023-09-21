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
