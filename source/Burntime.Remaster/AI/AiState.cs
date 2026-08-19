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
        // Legacy pre-v1.0.4 serialization field. The strategic AI no longer uses
        // the mode state machine, but the field must remain for old save games.
        protected Mode mode;
        protected StateLink<Location> headedLocation;
        protected AiSettings settings;
        protected int wait;
        protected StateLink<AiItemPool> itemPool;
        [System.Runtime.Serialization.OptionalField]
        protected StateLink<Player> retaliatingAgainst;
        [System.Runtime.Serialization.OptionalField]
        protected StateLink<Location> recentlyContestedCamp;
        [System.Runtime.Serialization.OptionalField]
        protected int retaliationUntilDay;
        [System.Runtime.Serialization.OptionalField]
        protected int contestedUntilDay;
        [System.Runtime.Serialization.OptionalField]
        protected int attackPlanUntilDay;
        // Runtime-only intent marker. Legacy serialized fields remain unchanged;
        // losing this hint on load merely causes the target to be reevaluated.
        [NonSerialized]
        bool strategicTargetWasNeutral;
        [System.Runtime.Serialization.OptionalField]
        protected StateLink<Location> deferredAttackCamp;
        [System.Runtime.Serialization.OptionalField]
        protected int deferredAttackUntilDay;
        [System.Runtime.Serialization.OptionalField]
        protected StateLink<Location> failedAttackCamp;
        [System.Runtime.Serialization.OptionalField]
        protected int failedAttackUntilDay;
        [System.Runtime.Serialization.OptionalField]
        protected int failedAttackGroupSize;
        [System.Runtime.Serialization.OptionalField]
        protected float failedAttackerStrength;
        [System.Runtime.Serialization.OptionalField]
        protected float failedDefenderStrength;
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
            StrategicAi.RunTurn(this);
        }

        #region strategic AI access
        internal ClassicGame RootGame => Game;
        internal Location Current => CurrentLocation;
        internal Location StrategicTarget
        {
            get => headedLocation;
            set
            {
                headedLocation = value;
                if (value == null)
                {
                    attackPlanUntilDay = 0;
                    strategicTargetWasNeutral = false;
                }
            }
        }
        internal bool HasSettlementPlan => strategicTargetWasNeutral && StrategicTarget != null;
        internal void SetSettlementTarget(Location location)
        {
            headedLocation = location;
            attackPlanUntilDay = 0;
            strategicTargetWasNeutral = location != null;
        }
        internal AiItemPool Pool => ItemPool;
        internal AiSettings Configuration => settings;
        internal int OwnedCampCount => CampCount;
        internal int HumanCampBenchmark => MaxHumanCampCount;
        internal bool HasHumanPlayers => RootGame.World.Players.Any(candidate =>
            candidate.Type == PlayerType.Human);
        internal bool IsRetaliatingAgainst(Player opponent) =>
            retaliatingAgainst != null && retaliatingAgainst.Object == opponent &&
            RootGame.World.Day <= retaliationUntilDay;
        internal bool WasRecentlyContested(Location location) =>
            recentlyContestedCamp != null && recentlyContestedCamp.Object == location &&
            RootGame.World.Day <= contestedUntilDay;
        internal bool IsAttackPlanExpired => attackPlanUntilDay > 0 &&
            RootGame.World.Day > attackPlanUntilDay;
        internal bool HasAttackPlan => StrategicTarget != null &&
            !StrategicTarget.IsCity && StrategicTarget.Player != null &&
            StrategicTarget.Player != Player;
        internal bool IsAttackTargetDeferred(Location location) =>
            deferredAttackCamp != null && deferredAttackCamp.Object == location &&
            RootGame.World.Day <= deferredAttackUntilDay;
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

        internal Character StationTradeFollower()
        {
            if (!IsHome || Player.Group.Count <= 2)
                return null;
            Character npc = Player.Group
                .Where(character => character != Player.Character)
                .OrderBy(character => character.Items.Sum(item => item.TradeValue))
                .ThenBy(character => character.AttackValue + character.DefenseValue)
                .FirstOrDefault();
            if (npc != null)
                JoinCamp(npc);
            return npc;
        }

        internal Character RecallCampFollower(int criticalGarrisonTarget)
        {
            if (!IsHome)
                return null;
            int minimumGuards = ReinforcementTask.IsCriticalCamp(this, CurrentLocation)
                ? criticalGarrisonTarget
                : 1;
            Character npc = CurrentLocation.CampNPC
                .Where(character => character.Player == Player && !character.IsDead)
                .OrderByDescending(character => character.AttackValue + character.DefenseValue)
                .FirstOrDefault();
            if (npc == null || CurrentLocation.CampNPC.Count(character =>
                    character.Player == Player && !character.IsDead) <= minimumGuards)
                return null;
            npc.LeaveCamp();
            return npc;
        }

        internal Character MobilizeCampFollower(int minimumGuards)
        {
            if (!IsHome)
                return null;
            Character npc = CurrentLocation.CampNPC
                .Where(character => character.Player == Player && !character.IsDead)
                .OrderByDescending(character => character.AttackValue + character.DefenseValue)
                .FirstOrDefault();
            if (npc == null || CurrentLocation.CampNPC.Count(character =>
                    character.Player == Player && !character.IsDead) <= minimumGuards)
                return null;
            npc.LeaveCamp();
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

        internal void MarkRecentlyCaptured(Location location, AiPolicy policy)
        {
            recentlyContestedCamp = location;
            contestedUntilDay = RootGame.World.Day + policy.ContestedCampMemoryTurns;
            ClearFailedAttack(location);
            wait = System.Math.Max(wait, policy.AttackCooldownTurns);
        }

        internal void StartAttackPlan(Location location, AiPolicy policy)
        {
            if (StrategicTarget == location && attackPlanUntilDay > 0)
                return;
            headedLocation = location;
            strategicTargetWasNeutral = false;
            attackPlanUntilDay = RootGame.World.Day + policy.AttackPlanTurns;
            AiTelemetry.Report(Player,
                $"started attack plan for {location.Title} with {policy.AttackPlanTurns} days to prepare");
        }

        internal void MarkAttackPlanReady(Location location)
        {
            if (StrategicTarget != location || attackPlanUntilDay <= 0)
                return;
            attackPlanUntilDay = 0;
            AiTelemetry.Report(Player,
                $"attack group for {location.Title} is ready; preparation deadline removed");
        }

        internal void DeferExpiredAttackPlan(Location location, AiPolicy policy)
        {
            deferredAttackCamp = location;
            deferredAttackUntilDay = RootGame.World.Day + policy.AttackPlanRetryDelay;
            StrategicTarget = null;
        }

        internal void RecordFailedAttack(
            Location location,
            int groupSize,
            float attackerStrength,
            float defenderStrength,
            AiPolicy policy)
        {
            failedAttackCamp = location;
            failedAttackUntilDay = RootGame.World.Day + policy.FailedAttackMemoryTurns;
            failedAttackGroupSize = groupSize;
            failedAttackerStrength = attackerStrength;
            failedDefenderStrength = defenderStrength;
            AiTelemetry.Report(Player,
                $"will reconsider {location.Title} only after recruiting, re-equipping, or weakening its defenders");
        }

        internal bool HasImprovedSinceFailedAttack(
            Location location,
            int groupSize,
            float attackerStrength,
            float defenderStrength)
        {
            if (failedAttackCamp == null || failedAttackCamp.Object != location ||
                RootGame.World.Day > failedAttackUntilDay)
                return true;

            return groupSize > failedAttackGroupSize ||
                attackerStrength >= failedAttackerStrength * 1.10f ||
                defenderStrength <= failedDefenderStrength * 0.85f;
        }

        void ClearFailedAttack(Location location)
        {
            if (failedAttackCamp == null || failedAttackCamp.Object != location)
                return;
            failedAttackCamp = null;
            failedAttackUntilDay = 0;
            failedAttackGroupSize = 0;
            failedAttackerStrength = 0;
            failedDefenderStrength = 0;
        }

        internal void RecordAttack(Character attacker, Character defender)
        {
            if (attacker.Player == null || defender.Player != Player || attacker.Player == Player)
                return;

            recentlyContestedCamp = defender.Location;
            contestedUntilDay = RootGame.World.Day + AiPolicy.ForDifficulty(
                RootGame.World.Difficulty).ContestedCampMemoryTurns;

            if (attacker.Player.Type != PlayerType.Human)
                return;
            retaliatingAgainst = attacker.Player;
            retaliationUntilDay = RootGame.World.Day + AiPolicy.ForDifficulty(
                RootGame.World.Difficulty).RetaliationTurns;
            AiTelemetry.Report(Player,
                $"will retaliate against {attacker.Player.Name} after the attack at {defender.Location.Title}");
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

        internal bool RecoverAtSafeLocation(AiPolicy policy)
        {
            if (!IsHome && !CurrentLocation.IsCity)
                return false;

            LocalOpportunities.ConsumeAvailableSupplies(this);
            bool usedCheat = false;

            bool lowFood = Player.Group.Any(character => character.Food <= 2) &&
                Player.Group.GetFoodReserve() + Player.Group.GetFoodInInventory() < Player.Group.Count * 2;
            bool lowWater = Player.Group.Any(character => character.Water <= 1) &&
                Player.Group.GetWaterReserve() + Player.Group.GetWaterInInventory() < Player.Group.Count;

            if (lowFood && !Player.Character.Items.Contains("item_meat") && !Player.Character.Items.IsFull)
            {
                Player.Character.Items.Add(Game.ItemTypes["item_meat"].Generate());
                usedCheat = true;
            }
            if (lowWater && !HasWaterContainer(Player.Character) && !Player.Character.Items.IsFull)
            {
                Item container = ItemPool.HasWaterContainer()
                    ? ItemPool.GetBestWaterContainer()
                    : Game.ItemTypes["item_full_wineskin"].Generate();
                Player.Character.Items.Add(container);
                usedCheat = true;
            }

            // Real carried and camp supplies are consumed first. The bounded safe-location
            // recovery remains a last-resort safeguard for otherwise stranded AI groups.
            if (Player.Group.Any(character => character.Food <= 3))
            {
                foreach (Character character in Player.Group)
                    character.Food = System.Math.Max(character.Food, policy.SafeFoodFloor);
                usedCheat = true;
            }
            if (Player.Group.Any(character => character.Water <= 2))
            {
                foreach (Character character in Player.Group)
                    character.Water = System.Math.Max(character.Water, policy.SafeWaterFloor);
                usedCheat = true;
            }
            foreach (Character character in Player.Group)
            {
                if (character.Health < 40)
                {
                    character.Health = System.Math.Min(100, character.Health + policy.SafeHealing);
                    usedCheat = true;
                }
            }
            return usedCheat;
        }

        internal void ResetWait()
        {
            wait = settings.MaxInterval > settings.MinInterval
                ? Burntime.Platform.Math.Random.Next(settings.MinInterval, settings.MaxInterval)
                : settings.MinInterval;
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

            CollectGroundItems();

            EquipWaterContainers(Player.Group.Where(character => character != Player.Character));
            if (IsHome)
                EquipWaterContainers(
                    CurrentLocation.CampNPC.Where(character => character.Player == Player),
                    useCampStorage: true);

        }

        internal void CollectGroundItems()
        {
            if (CurrentLocation.Player != null && CurrentLocation.Player != Player)
                return;
            CollectGroundItems(CurrentLocation.Items.ToArray());
        }

        internal void CollectCombatLoot(IEnumerable<Item> dropped)
        {
            Item[] combatDrops = dropped
                .Where(item => CurrentLocation.Items.Any(ground => ground == item))
                .Distinct()
                .ToArray();
            CollectGroundItems(combatDrops);
        }

        void CollectGroundItems(IEnumerable<Item> groundItems)
        {
            // Strategic equipment is shared through the AI pool. Keep other goods as real items so
            // they can be used for future trading instead of disappearing into the abstract pool.
            foreach (Item item in groundItems)
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
                else if (LocalOpportunities.TryReplaceCargo(
                    this, item, out Item replaced, out Character carrier))
                {
                    CurrentLocation.Items.Remove(item);
                    carrier.Items.Add(item);
                    CurrentLocation.Items.Add(replaced);
                    AiTelemetry.Report(Player,
                        $"replaced cargo {replaced.ID} with higher-value ground find {item.ID}");
                }
                else if (IsHome && CurrentLocation.Rooms.Any(room => !room.Items.IsFull))
                {
                    CurrentLocation.StoreItemRandom(item);
                    CurrentLocation.Items.Remove(item);
                }
            }
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
            bool preferProductionUpgrade = LocalOpportunities.ShouldPreferProductionAtCamp(this, CurrentLocation) &&
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
            if (!hasProductionTool && !ExpansionTask.CanBootstrapCamp(this, location))
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
            Character[] available = CurrentLocation.Characters
                .Where(character => !character.IsDead && !character.IsHired && character.IsHuman && !character.IsTrader)
                .ToArray();
            if (available.Length == 0)
                return null;

            (int minimum, int maximum) = RootGame.World.Difficulty switch
            {
                0 => (0, 40),
                1 => (20, 60),
                _ => (40, int.MaxValue)
            };
            Character[] preferred = available
                .Where(character => character.Experience >= minimum && character.Experience <= maximum)
                .ToArray();
            Character[] candidates = preferred.Length > 0 ? preferred : available;
            return candidates[Burntime.Platform.Math.Random.Next(candidates.Length)];
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

        #endregion
    }
}
