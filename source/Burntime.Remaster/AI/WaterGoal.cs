using System;
using System.Collections.Generic;
using Burntime.Remaster.Logic;
using Burntime.Framework;
using Burntime.Framework.States;

namespace Burntime.Remaster.AI
{
    [Serializable]
    class WaterGoal : /*StateObject, */IAiGoal 
    {
        bool inProgress = false;
        public bool InProgress { get { return inProgress; } }

        StateLink<Player> player;
        protected Player Player { get { return player; } }

        public WaterGoal(Player player)
        {
            this.player = player;
        }

        public float CalculateScore()
        {
            var ch = Player.Character;
            var group = Player.Group;
            float score = 0;

            // water reserves in the group
            score += (group.GetWaterReserve() + group.GetWaterInInventory()) / (float)group.Count;

            return score;
        }

        public void AlwaysDo()
        {
            // refresh from free sources
            if (Player.Location.Source != null)
                Player.Location.Source.Reserve = Player.Group.Drink(Player.Character, Player.Location.Source.Reserve);

            // refill empty items in inventory
            foreach (var item in Player.Group.GetEmptyWaterItems())
                Player.Location.Source.RefillItem(item);

            // Keep empty containers with their carriers for the next usable water source.
        }

        public void Act()
        {
            //
        }
    }
}
