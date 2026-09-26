using System;
using System.Linq;
using Burntime.Framework.States;
using Burntime.Platform;

namespace Burntime.Remaster.Logic.Interaction
{
    [Serializable]
    public class VictoryCondition : StateObject
    {
        [NonSerialized]
        Player? shortcutWinner;

        internal bool CanOfferLastRivalVictory(Player player)
        {
            ClassicGame game = (ClassicGame)container.Root;
            return player.Type == PlayerType.Human && IsAlive(player) &&
                game.World.Players.Contains(player) &&
                !game.World.Players.Any(other => other != player && IsAlive(other)) &&
                !Process(player);
        }

        internal void AcceptLastRivalVictory(Player player)
        {
            if (CanOfferLastRivalVictory(player))
                shortcutWinner = player;
        }

        static bool IsAlive(Player player) => !player.IsDead && !player.Character.IsDead;

        public bool Process(Player player)
        {
            if (!IsAlive(player))
                return false;
            if (shortcutWinner == player)
                return true;

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
