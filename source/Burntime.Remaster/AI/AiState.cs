using System;
using System.Collections.Generic;
using Burntime.Remaster.Logic;
using Burntime.Framework;
using Burntime.Framework.States;
using System.Linq;

namespace Burntime.Remaster.AI
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
        /// Process AI player turn.
        /// </summary>
        public void Turn()
        {
            UpdateItems();

            List<IAiGoal> goals = new List<IAiGoal>();
            goals.Add(new WaterGoal(player));

            foreach (var goal in goals)
                goal.AlwaysDo();

            StrategicAiEconomy.Run(this);

            StrategicAiDecision decision = StrategicAiPlanner.Choose(this);
            StrategicAiExecutor.Execute(this, decision);
            DebugOutput(goals);
        }

        #region strategic AI access
        internal ClassicGame RootGame => Game;
        internal Location Current => CurrentLocation;
        internal Location StrategicTarget
        {
            get => headedLocation;
            set => headedLocation = value;
        }
        internal AiItemPool Pool => ItemPool;
        internal AiSettings Configuration => settings;
        internal int OwnedCampCount => CampCount;
        internal int HumanCampBenchmark => MaxHumanCampCount;
        internal int WaitTurns
        {
            get => wait;
            set => wait = value;
        }

        internal bool CanClaim(Location location) => CanCreateCamp(location);
        internal bool CanStationCamp() => Player.Group.Count > 1 || CanRecruit(allowGeneratedPayment: CurrentLocation.IsCity);
        internal bool HasHireableNpc() => CanHireNpc();
        internal bool CanRecruit(bool allowGeneratedPayment)
        {
            Character candidate = GetHireableNpc();
            if (candidate == null || Player.Group.Count >= Group.MAX_PEOPLE)
                return false;
            if (candidate.HireItems.Count == 0 || allowGeneratedPayment)
                return true;
            return candidate.HireItems.Any(type => Player.Character.Items.Find(type) != null);
        }
        internal string[] AvailableProducts(Location location) => GetAvailableProducts(location);

        internal bool NeedsCampImprovement()
        {
            if (!IsHome)
                return false;
            string[] products = GetAvailableProducts(CurrentLocation);
            if (!ItemPool.HasTrap(products))
                return false;

            bool HasCompatibleProduction(Item item) => item.Type.Production != null &&
                products.Contains(item.Type.Production.Produce.ID);
            return !CurrentLocation.Rooms.SelectMany(room => room.Items).Any(HasCompatibleProduction) &&
                !CurrentLocation.CampNPC.SelectMany(character => character.Items).Any(HasCompatibleProduction);
        }

        internal Character Recruit(bool allowGeneratedPayment)
        {
            return HireNpc(allowGeneratedPayment);
        }

        internal Character StationSurplusFollower()
        {
            if (!IsHome || Player.Group.Count <= 1)
                return null;
            Character npc = Player.Group
                .Where(character => character != Player.Character)
                .OrderBy(character => character.Class switch
                {
                    CharClass.Doctor => 3,
                    CharClass.Technician => 3,
                    CharClass.Mercenary => 2,
                    _ => 1
                })
                .ThenBy(character => character.AttackValue + character.DefenseValue)
                .FirstOrDefault();
            if (npc != null)
                JoinCamp(npc);
            return npc;
        }

        internal Character SelectCampNpc()
        {
            Character recruit = CanHireNpc() ? HireNpc(allowGeneratedPayment: CurrentLocation.IsCity) : null;
            if (recruit != null)
                return recruit;
            return Player.Group.Count > 1 ? Player.Group[1] : null;
        }

        internal void CreateCamp(Character npc)
        {
            JoinCamp(npc);
            StrategicTarget = null;
            ResetWait();
        }

        internal bool ImproveCamp()
        {
            if (!IsHome || !ItemPool.HasTrap(GetAvailableProducts(CurrentLocation)))
                return false;

            Item trap = ItemPool.GetBestTrap(GetAvailableProducts(CurrentLocation));
            if (trap == null)
                return false;
            CurrentLocation.StoreItemRandom(trap);
            return true;
        }

        internal void AddEmergencySupplies(ClassicAiPolicy policy)
        {
            if (!IsHome && !CurrentLocation.IsCity)
                return;

            bool lowFood = Player.Group.Any(character => character.Food <= 2) &&
                Player.Group.GetFoodReserve() + Player.Group.GetFoodInInventory() < Player.Group.Count * 2;
            bool lowWater = Player.Group.Any(character => character.Water <= 1) &&
                Player.Group.GetWaterReserve() + Player.Group.GetWaterInInventory() < Player.Group.Count;

            if (lowFood && !Player.Character.Items.Contains("item_meat") && !Player.Character.Items.IsFull)
                Player.Character.Items.Add(Game.ItemTypes["item_meat"].Generate());
            if (lowWater && !HasWaterContainer(Player.Character) && !Player.Character.Items.IsFull)
            {
                Item container = ItemPool.HasWaterContainer()
                    ? ItemPool.GetBestWaterContainer()
                    : Game.ItemTypes["item_full_wineskin"].Generate();
                Player.Character.Items.Add(container);
            }

            // Safe locations provide bounded recovery, not the former full refill/heal.
            foreach (Character character in Player.Group)
            {
                character.Food = System.Math.Max(character.Food, policy.SafeFoodFloor);
                character.Water = System.Math.Max(character.Water, policy.SafeWaterFloor);
                character.Health = System.Math.Min(100, character.Health + policy.SafeHealing);
            }
        }

        internal void ResetWait()
        {
            wait = settings.MaxInterval > settings.MinInterval
                ? Burntime.Platform.Math.Random.Next(settings.MinInterval, settings.MaxInterval)
                : settings.MinInterval;
        }
        #endregion

        #region debug
        private void DebugOutput(IEnumerable<IAiGoal> goals)
        {
            var ch = player.Object.Character;
            DebugLog("", player.Object.IsDead ? "dead" : ("in " + ch.Location.Title));
            DebugLog(" mode", mode.ToString());
            DebugLog(" values", "health=" + ch.Health + " food=" + ch.Food + " water=" + ch.Water);
            DebugLog(" npcs", "count=" + player.Object.Group.Count);
            DebugLog(" items", ch.Items.ToString());

            foreach (var goal in goals)
                DebugLog(" " + goal.ToString(), "score=" + goal.CalculateScore());
        }

        private void DebugLog(string key, string info)
        {
#warning TODO SlimDX/Mono debug infos
            //Burntime.Platform.Debug.SetInfo("AI " + player.Object.Character.Name + key, info);
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

            // Strategic equipment is shared through the AI pool. Keep other goods as real items so
            // they can be used for future trading instead of disappearing into the abstract pool.
            foreach (Item item in CurrentLocation.Items.ToArray())
            {
                if (AiItemPool.Accepts(item.Type))
                {
                    ItemPool.Insert(item);
                    CurrentLocation.Items.Remove(item);
                }
                else if (ItemPool.TryReserveConstructionMaterial(item))
                {
                    // Preserve the first copy even when the travel group deliberately
                    // kept only limited room for unexpected ground loot.
                    CurrentLocation.Items.Remove(item);
                }
                else if (TryStoreInGroup(item))
                {
                    CurrentLocation.Items.Remove(item);
                }
                else if (StrategicAiEconomy.TryReplaceCargo(
                    this, item, out Item replaced, out Character carrier))
                {
                    CurrentLocation.Items.Remove(item);
                    carrier.Items.Add(item);
                    CurrentLocation.Items.Add(replaced);
                    StrategicAiTelemetry.Report(Player,
                        $"replaced cargo {replaced.ID} with higher-value ground find {item.ID}");
                }
                else if (IsHome && CurrentLocation.Rooms.Any(room => !room.Items.IsFull))
                {
                    CurrentLocation.StoreItemRandom(item);
                    CurrentLocation.Items.Remove(item);
                }
            }

            EquipWaterContainers(Player.Group.Where(character => character != Player.Character));
            if (IsHome)
                EquipWaterContainers(
                    CurrentLocation.CampNPC.Where(character => character.Player == Player),
                    useCampStorage: true);

        }

        private bool TryStoreInGroup(Item item)
        {
            Character carrier = Player.Group.FirstOrDefault(character => !character.Items.IsFull);
            return carrier != null && carrier.Items.Add(item);
        }

        private static bool HasWaterContainer(Character character) =>
            character.Items.Any(item => AiItemPool.IsWaterContainer(item.Type));

        private void EquipWaterContainers(IEnumerable<Character> characters, bool useCampStorage = false)
        {
            foreach (Character character in characters)
            {
                if (HasWaterContainer(character) || character.Items.IsFull)
                    continue;

                Item container = ItemPool.HasWaterContainer()
                    ? ItemPool.GetBestWaterContainer()
                    : useCampStorage ? TakeBestStoredWaterContainer() : null;
                if (container != null)
                    character.Items.Add(container);
            }
        }

        private Item TakeBestStoredWaterContainer()
        {
            var stored = CurrentLocation.Rooms
                .SelectMany(room => room.Items.Select(item => new { Room = room, Item = item }))
                .Where(entry => AiItemPool.IsWaterContainer(entry.Item.Type))
                .OrderByDescending(entry => entry.Item.WaterValue > 0
                    ? entry.Item.WaterValue
                    : entry.Item.Type.Full.WaterValue)
                .ThenByDescending(entry => entry.Item.WaterValue)
                .FirstOrDefault();
            if (stored == null)
                return null;

            stored.Room.Items.Remove(stored.Item);
            return stored.Item;
        }
        #endregion

        #region protected camp management methods
        /// <summary>
        /// Create a camp at current location.
        /// </summary>
        /// <param name="npc">NPC to join camp</param>
        protected void JoinCamp(Character npc)
        {
            EquipWaterContainers(new[] { npc }, useCampStorage: true);

            if (CurrentLocation.Danger != null && !npc.Items.IsFull)
            {
                Item protection = CurrentLocation.Danger.Type == "radiation"
                    ? ItemPool.GetProtectionSuit()
                    : ItemPool.GetGasMask();
                if (protection != null)
                {
                    npc.Items.Add(protection);
                    npc.Protection = protection;
                }
            }

            // join camp
            npc.JoinCamp();

            // Add a real compatible production tool. A carried weapon such as a knife can
            // remain on the guard and serve both defense and maggot production.
            Item existingTool = CurrentLocation.Rooms.SelectMany(room => room.Items)
                .Concat(npc.Items)
                .FirstOrDefault(item => item.Type.Production != null &&
                    GetAvailableProducts(CurrentLocation).Contains(item.Type.Production.Produce.ID));
            bool preferProductionUpgrade = StrategicAiEconomy.ShouldPreferProductionAtCamp(this, CurrentLocation) &&
                ItemPool.HasHigherValueTrap(existingTool?.Type.Production?.Produce.TradeValue ?? -1,
                    GetAvailableProducts(CurrentLocation));
            Item trap = existingTool == null || preferProductionUpgrade
                ? ItemPool.HasTrap(GetAvailableProducts(CurrentLocation))
                    ? ItemPool.GetBestTrap(GetAvailableProducts(CurrentLocation))
                    : existingTool == null ? TakeCompatibleGroupProduction(CurrentLocation) : null
                : null;
            if (trap != null)
            {
                if (trap.Type.IsClass("weapon") && !npc.Items.IsFull)
                {
                    if (!npc.Items.Contains(trap))
                        npc.Items.Add(trap);
                    npc.Weapon = trap;
                }
                else if (CurrentLocation.Rooms.Count > 0)
                {
                    CurrentLocation.StoreItemRandom(trap);
                }
                else
                {
                    npc.Items.Add(trap);
                }
            }
            else if (existingTool?.DamageValue > 0)
            {
                npc.Weapon = existingTool;
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

            // A safe one-NPC camp may bootstrap from the location's base food yield.
            // Threatened camps still require real equipment before expansion.
            bool hasProductionTool = ItemPool.HasTrap(GetAvailableProducts(location)) ||
                FindCompatibleGroupProduction(location) != null;
            if (!hasProductionTool && !StrategicAiEconomy.CanBootstrapCamp(this, location))
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

        private Item FindCompatibleGroupProduction(Location location)
        {
            string[] products = GetAvailableProducts(location);
            return Player.Group.SelectMany(character => character.Items)
                .Where(item => item.Type.Production != null && products.Contains(item.Type.Production.Produce.ID))
                .OrderByDescending(item => item.Type.Production.Produce.FoodValue)
                .FirstOrDefault();
        }

        private Item TakeCompatibleGroupProduction(Location location)
        {
            Item item = FindCompatibleGroupProduction(location);
            if (item == null)
                return null;
            Character owner = Player.Group.First(character => character.Items.Contains(item));
            owner.Items.Remove(item);
            return item;
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
            bool hasDoctor = Player.Group.Any(character => character.Class == CharClass.Doctor);
            return CurrentLocation.Characters
                .Where(character => !character.IsDead && !character.IsHired && character.IsHuman && !character.IsTrader)
                .OrderByDescending(character => character.Class switch
                {
                    CharClass.Mercenary => 40,
                    CharClass.Doctor when !hasDoctor => 35,
                    CharClass.Technician => 25,
                    CharClass.Doctor => 15,
                    _ => 10
                })
                .ThenByDescending(character => character.Experience)
                .FirstOrDefault();
        }

        /// <summary>
        /// Hire NPC and add to group.
        /// </summary>
        /// <returns>hired NPC</returns>
        protected Character HireNpc(bool allowGeneratedPayment = true)
        {
            Character ch = GetHireableNpc();
            if (ch == null || Player.Group.Count >= Group.MAX_PEOPLE)
                return null;

            if (ch.HireItems.Count > 0)
            {
                Item payment = ch.HireItems
                    .Select(type => Player.Character.Items.Find(type))
                    .FirstOrDefault(item => item != null);

                if (payment == null && allowGeneratedPayment)
                {
                    if (Player.Character.Items.IsFull)
                    {
                        Item leastUseful = Player.Character.Items.OrderBy(item => item.TradeValue).First();
                        CurrentLocation.StoreItemRandom(leastUseful);
                        Player.Character.Items.Remove(leastUseful);
                    }

                    ItemType paymentType = ch.HireItems.OrderBy(type => type.TradeValue).First();
                    Player.Character.Items.Add(paymentType.Generate());
                }
                else if (payment == null)
                {
                    return null;
                }
            }

            // hire
            ch.Hire(Player);

            EquipWaterContainers(new[] { ch });

            // add weapon to npc
            if (ItemPool.HasWeapon() && !ch.Items.IsFull)
            {
                // An unarmed new follower needs the tool now. Knives still serve food
                // production later when that follower is stationed at a camp.
                Item weapon = ItemPool.GetBestWeapon(allowProductionTool: true);
                if (weapon != null)
                {
                    ch.Items.Add(weapon);
                    ch.SelectItem(weapon);
                }
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
