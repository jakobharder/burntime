
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
    #region public AI settings structure
    /// <summary>
    /// AI settings
    /// </summary>
    [Serializable]
    public struct AiSettings
    {
        /// <summary>
        /// Random interval to create camps. Minimum border.
        /// </summary>
        public int MinInterval;

        /// <summary>
        /// Random interval to create camps. Maximum border.
        /// </summary>
        public int MaxInterval;

        /// <summary>
        /// Maximum number of camps in advance of human players.
        /// </summary>
        public int MaxAdvance;
    }
    #endregion

    /// <summary>
    /// AI processing StateObject
    /// </summary>
    [Serializable]
    class ClassicAiState : Burntime.Framework.States.AiState
    {
        protected enum Mode
        {
            None,
            LookForNextCamp,
            HireNpc,
            WaitInterval
        }

        #region protected attributes
        protected StateLink<Player> player;
        protected Mode mode;
        protected StateLink<Location> headedLocation;
        protected AiSettings settings;
        protected int wait;
        protected StateLink<AiItemPool> itemPool;
        #endregion

        #region protected properties
        /// <summary>
        /// player state relative to this ai state
        /// </summary>
        public Player Player
        {
            get { return player; }
        }

        public Location HeadedLocation
        {
            get { return headedLocation; }
        }

        /// <summary>
        /// Weapon, protection, trap items pool
        /// </summary>
        protected AiItemPool ItemPool
        {
            get { return itemPool; }
        }
        #endregion

        #region protected initialize
        /// <summary>
        /// StateObject initialization
        /// </summary>
        /// <param name="parameter">Player, AiSettings</param>
        protected override void InitInstance(object[] parameter)
        {
            base.InitInstance(parameter);

            if (parameter.Length != 2)
                throw new BurntimeLogicException();

            player = parameter[0] as Player;
            if (player == null)
                throw new BurntimeLogicException();
            settings = (AiSettings)parameter[1];

            mode = Mode.WaitInterval;
            wait = Burntime.Platform.Math.Random.Next(settings.MinInterval, settings.MaxInterval);

            itemPool = container.Create<AiItemPool>();
        }
        #endregion

        /// <summary>
        /// Make some logic preparations.
        /// </summary>
        protected void FirstTurn()
        {
            // add some traps, weapons to pool
            ItemPool.Insert(Game.ItemTypes["item_knife"]);
            ItemPool.Insert(Game.ItemTypes["item_knife"]);
            ItemPool.Insert(Game.ItemTypes["item_knife"]);
            ItemPool.Insert(Game.ItemTypes["item_rat_trap"]);
            ItemPool.Insert(Game.ItemTypes["item_snake_trap"]);
        }

        /// <summary>
        /// Process AI player turn.
        /// </summary>
        public void Turn()
        {
            if (Game.World.Day == 1)
                FirstTurn();

            bool busy = true;

            // update items
            UpdateItems();

            // hire npc if noone in group
            if (Player.Group.Count == 1)
            {
                mode = Mode.HireNpc;
            }

            while (busy)
            {
                switch (mode)
                {
                    case Mode.None:
                        busy = TurnModeNone();
                        break;
                    case Mode.LookForNextCamp:
                        busy = TurnModeLookForNextCamp();
                        break;
                    case Mode.HireNpc:
                        busy = TurnModeHireNpc();
                        break;
                    case Mode.WaitInterval:
                        busy = TurnModeWaitInterval();
                        break;
                }
            }

            if (IsHome || CurrentLocation.IsCity)
            {
                // refresh meat, wineskin
                RefreshGroupReserves();

                // refresh health, food and water
                RefreshGroupAttributes();
            }

            DebugOutput();
        }

        #region debug
        private void DebugOutput()
        {
            var ch = player.Object.Character;
            DebugLog("", player.Object.IsDead ? "dead" : ("in " + ch.Location.Title));
            DebugLog(" mode", mode.ToString());
            DebugLog(" values", "health=" + ch.Health + " food=" + ch.Food + " water=" + ch.Water);
            DebugLog(" npcs", "count=" + player.Object.Group.Count);
            DebugLog(" items", ch.Items.ToString());
        }

        private void DebugLog(string key, string info)
        {
            Burntime.Platform.Debug.SetInfo("AI " + player.Object.Character.Name + key, info);
        }
        #endregion

        #region turn modes
        private bool TurnModeNone()
        {
            mode = Mode.LookForNextCamp;
            return true;
        }

        /// <summary>
        /// Turn mode - Look for next camp
        /// </summary>
        /// <returns>true if no further turn processing is needed</returns>
        private bool TurnModeLookForNextCamp()
        {
            if (MaxHumanCampCount + settings.MaxAdvance <= CampCount)
            {
                mode = Mode.None;
                return false;
            }

            // if not at home, enemy camp or in a city and resources for a camp are available
            if (CanCreateCamp(CurrentLocation))
            {
                // claim current camp
                Character npc = GetNpcForCamp();
                if (npc != null)
                    JoinCamp(npc);

                if (Player.Group.Count == 1)
                {
                    // used group member to hire, find a new one
                    mode = Mode.HireNpc;
                    return true;
                }
                else
                {
                    // wait some time
                    mode = Mode.WaitInterval;
                    wait = Burntime.Platform.Math.Random.Next(settings.MinInterval, settings.MaxInterval);
                }
            }
            else
            {
                // find next possible camp location
                headedLocation = NearestFreeCamp();
                if (headedLocation != null)
                    Player.Travel(headedLocation);
                return false;
            }

            return false;
        }

        private bool TurnModeHireNpc()
        {
            if (CanHireNpc())
            {
                Character ch = HireNpc();

                mode = Mode.LookForNextCamp;
                return true;
            }
            else
            {
                headedLocation = NearestCity();
                if (headedLocation != null)
                    Player.Travel(headedLocation);
            }

            return false;
        }

        private bool TurnModeWaitInterval()
        {
            wait--;
            // has finished waiting
            if (wait <= 0)
            {
                // go in camp creating mode only if not too much camps controlled
                if (MaxHumanCampCount + settings.MaxAdvance > CampCount)
                {
                    mode = Mode.LookForNextCamp;
                    return true;
                }

                return false;
            }

            return false;
        }
        #endregion

        #region protected helper methods
        private ClassicGame Game
        {
            get { return (container.Root as ClassicGame); }
        }

        private Location CurrentLocation
        {
            get { return this.Player.Location; }
        }

        private bool IsHome
        {
            get { return CurrentLocation.Player == Player; }
        }

        private int CampCount
        {
            get
            {
                int count = 0;

                foreach (Location loc in Game.World.Locations)
                {
                    if (loc.Player == Player)
                        count++;
                }

                return count;
            }
        }

        private int MaxHumanCampCount
        {
            get
            {
                int max = 0;

                foreach (PlayerState player in Game.Player)
                {
                    int count = 0;

                    if (player.AiState == null)
                    {
                        foreach (Location loc in Game.World.Locations)
                        {
                            if (loc.Player == player)
                                count++;
                        }
                    }

                    max = System.Math.Max(count, max);
                }

                return max;
            }
        }

        private Location NearestFreeCamp()
        {
            int days = 0;
            List<Location> list = new List<Location>();
            Location next;
            if (null != NearestFreeCamp(CurrentLocation, out days, ref list, out next) && next != null)
                return next;

            return null;
        }

        private Location NearestFreeCamp(Location current, out int days, ref List<Location> list, out Location next)
        {
            next = null;

            if (list.Contains(current))
            {
                days = 0;
                return null;
            }

            list.Add(current);

            int shortest = 9999;
            Location nearest = null;
            for (int i = 0; i < current.WayLengths.Length; i++)
            {
                if (current.WayLengths[i] > 0 && current.WayLengths[i] < shortest &&
                    CanCreateCamp(current.Neighbors[i]) && 
                    Player.CanTravel(current, current.Neighbors[i]))
                {
                    shortest = current.WayLengths[i];
                    nearest = current.Neighbors[i];
                }
            }

            if (nearest != null)
            {
                days = shortest;
                next = nearest;
                return nearest;
            }

            shortest = -1;
            for (int i = 0; i < current.WayLengths.Length; i++)
            {
                if (current.WayLengths[i] == 0)
                    continue;

                // only travel through if not controlled by enemy
                if (!Player.CanTravel(current, current.Neighbors[i]))
                    continue;

                days = 0;
                Location dummy;
                Location loc = NearestFreeCamp(current.Neighbors[i], out days, ref list, out dummy);
                if (loc != null)
                {
                    if (shortest == -1 || current.WayLengths[i] + days < shortest)
                    {
                        shortest = current.WayLengths[i] + days;
                        nearest = loc;
                        next = current.Neighbors[i];
                    }
                }
            }

            if (nearest != null)
            {
                days = shortest;
                return nearest;
            }

            days = 0;
            return null;
        }

        private Location NearestCity()
        {
            int days = 0;
            List<Location> list = new List<Location>();
            Location next;
            if (null != NearestCity(CurrentLocation, out days, ref list, out next) && next != null)
                return next;

            return null;
        }

        private Location NearestCity(Location current, out int days, ref List<Location> list, out Location next)
        {
            next = null;

            if (list.Contains(current))
            {
                days = 0;
                return null;
            }

            list.Add(current);

            int shortest = 9999;
            Location nearest = null;
            for (int i = 0; i < current.WayLengths.Length; i++)
            {
                if (current.WayLengths[i] > 1 && current.WayLengths[i] < shortest &&
                    current.Neighbors[i].IsCity)
                {
                    shortest = current.WayLengths[i];
                    nearest = current.Neighbors[i];
                }
            }

            if (nearest != null)
            {
                days = shortest;
                next = nearest;
                return nearest;
            }

            shortest = -1;
            for (int i = 0; i < current.WayLengths.Length; i++)
            {
                if (current.WayLengths[i] == 0)
                    continue;

                // only travel through if not controlled by enemy
                if (current.Neighbors[i].Player != null && current.Neighbors[i].Player != Player)
                    continue;

                days = 0;
                Location dummy;
                Location loc = NearestFreeCamp(current.Neighbors[i], out days, ref list, out dummy);
                if (loc != null &&
                    (loc.Player == null || loc.Player == Player)) // only travel through if not controlled by enemy
                {
                    if (shortest == -1 || current.WayLengths[i] + days < shortest)
                    {
                        shortest = current.WayLengths[i] + days;
                        nearest = loc;
                        next = current.Neighbors[i];
                    }
                }
            }

            if (nearest != null)
            {
                days = shortest;
                return nearest;
            }

            days = 0;
            return null;
        }
        #endregion

        #region item management
        /// <summary>
        /// Item management, update pool, ...
        /// </summary>
        private void UpdateItems()
        {
#warning after loading savegame this may be null, why?
            if (itemPool == null)
                itemPool = container.Create<AiItemPool>();

            // add items from ground to item pool
            ItemPool.Insert(CurrentLocation.Items);

            // remove all items from ground
            CurrentLocation.Items.Clear();

            int turn = Game.World.Day;

            // add weapons to pool (TODO make dependent on difficulty)
            if (turn % 3 == 0)
                ItemPool.Insert(Game.ItemTypes["item_knife"]);
            if (turn % 10 == 0)
                ItemPool.Insert(Game.ItemTypes["item_axe"]);
            if (turn % 15 == 0)
                ItemPool.Insert(Game.ItemTypes["item_pitchfork"]);
            if (turn % 20 == 0)
                ItemPool.Insert(Game.ItemTypes["item_loaded_rifle"]);
            if (turn % 17 == 0)
                ItemPool.Insert(Game.ItemTypes["item_loaded_pistol"]);

            // add traps to pool
            if (turn % 3 == 0)
                ItemPool.Insert(Game.ItemTypes["item_knife"]);
            if (turn % 7 == 0)
                ItemPool.Insert(Game.ItemTypes["item_rat_trap"]);
            if (turn % 11 == 0)
                ItemPool.Insert(Game.ItemTypes["item_snake_trap"]);
            if (turn % 14 == 0)
                ItemPool.Insert(Game.ItemTypes["item_trap"]);

            // add protection items to pool
            if (turn % 25 == 0)
                ItemPool.Insert(Game.ItemTypes["item_gas_mask"]);
            if (turn % 50 == 0)
                ItemPool.Insert(Game.ItemTypes["item_protection_suit"]);
        }
        #endregion

        #region protected camp management methods
        /// <summary>
        /// Create a camp at current location.
        /// </summary>
        /// <param name="npc">NPC to join camp</param>
        protected void JoinCamp(Character npc)
        {
            // join camp
            npc.JoinCamp();

            // add production
            if (ItemPool.HasTrap(GetAvailableProducts(CurrentLocation)))
            {
                Item trap = ItemPool.GetBestTrap(GetAvailableProducts(CurrentLocation));

                // put item into a room if available
                if (CurrentLocation.Rooms.Count > 0)
                {
                    CurrentLocation.StoreItemRandom(trap);
                }
                // otherwise add to NPC inventory
                else
                {
                    npc.Items.Add(trap);
                }
            }
        }

        /// <summary>
        /// Checks wether a sustainable camp can be created.
        /// </summary>
        /// <returns>true if possible</returns>
        /// <param name="location">location for camp</param>
        protected bool CanCreateCamp(Location location)
        {
            // not at cities
            if (location.IsCity)
                return false;

            // camp already exists
            if (location.Player != null)
                return false;

            // no appropriate trap available
            if (!ItemPool.HasTrap(GetAvailableProducts(location)))
                return false;

            // in case of hazards
            if (location.Danger != null)
            {
                if (location.Danger.Type == "gas")
                {
                    // no gas mask
                    if (!ItemPool.HasGasMask())
                        return false;
                }
                else if (location.Danger.Type == "radiation")
                {
                    // no protection suit
                    if (!ItemPool.HasProtectionSuit())
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get list of products available in current camp.
        /// </summary>
        /// <returns>list of products</returns>
        /// <param name="location">location</param>
        protected string[] GetAvailableProducts(Location location)
        {
            List<string> products = new List<string>();

            foreach (int i in location.AvailableProducts)
            {
                if (i == -1)
                    continue;

                products.Add(Game.Productions[i].Produce.ID);
            }

            return products.ToArray();
        }
        #endregion

        #region protected NPC group management methods
        /// <summary>
        /// Check wether a NPC is available for hire
        /// </summary>
        /// <returns>true if available</returns>
        protected bool CanHireNpc()
        {
            foreach (Character ch in CurrentLocation.Characters)
            {
                if (!ch.IsDead && !ch.IsHired && ch.IsHuman && !ch.IsTrader)
                {
                    // this one is available
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get a NPC that is available for hire
        /// </summary>
        /// <returns>NPC</returns>
        protected Character GetHireableNpc()
        {
            foreach (Character ch in CurrentLocation.Characters)
            {
                if (!ch.IsDead && !ch.IsHired && ch.IsHuman && !ch.IsTrader)
                {
                    // this one is available
                    return ch;
                }
            }

            return null;
        }

        /// <summary>
        /// Hire NPC and add to group.
        /// </summary>
        /// <returns>hired NPC</returns>
        protected Character HireNpc()
        {
            Character ch = GetHireableNpc();
            if (ch.HireItems.Count > 0)
            {
                // clear items to prevent required item not getting in
                Player.Character.Items.Clear();

                // add required item
                Player.Character.Items.Add(ch.HireItems.Last.Generate());
            }

            // hire
            ch.Hire(Player);

            // clear items
            ch.Items.Clear();

            // add weapon to npc
            if (ItemPool.HasWeapon())
            {
                Item weapon = ItemPool.GetBestWeapon();
                ch.Items.Add(weapon);
                ch.SelectItem(weapon);
            }

            return ch;
        }

        /// <summary>
        /// Get NPC for camp creation
        /// </summary>
        /// <returns>hired NPC or null</returns>
        protected Character GetNpcForCamp()
        {
            if (CanHireNpc())
                return HireNpc();
            else if (Player.Group.Count > 1)
                return Player.Group[1];

            return null;
        }

        /// <summary>
        /// Refresh health, food and water values of group members.
        /// </summary>
        protected void RefreshGroupAttributes()
        {
            // refresh some food/water
            Player.Group.Drink(null, 10);
            Player.Group.Eat(null, 10);

            // refresh some health
            Player.Group.Heal(null, 100);
        }

        /// <summary>
        /// Refresh food, water items of group.
        /// </summary>
        protected void RefreshGroupReserves()
        {
            // refresh meat
            if (!Player.Character.Items.Contains("item_meat"))
                Player.Character.Items.Add(Game.ItemTypes["item_meat"].Generate());

            // refresh wineskin
            if (!Player.Character.Items.Contains("item_empty_wineskin") &&
                !Player.Character.Items.Contains("item_full_wineskin"))
            {
                Player.Character.Items.Add(Game.ItemTypes["item_full_wineskin"].Generate());
            }

            if (Player.Character.Items.Contains("item_empty_wineskin"))
            {
                foreach (Item item in Player.Character.Items)
                {
                    if (item.Type == Game.ItemTypes["item_empty_wineskin"])
                        item.MakeFull();
                }
            }
        }
        #endregion
    }
}
