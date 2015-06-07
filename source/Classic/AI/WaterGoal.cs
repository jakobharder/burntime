
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
using Burntime.Classic.Logic;
using Burntime.Framework;
using Burntime.Framework.States;

namespace Burntime.Classic.AI
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

            // put still empty items into own camps storage
        }

        public void Act()
        {
            //
        }
    }
}
