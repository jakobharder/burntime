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
            HuntOneDogForMeat();
            UpdateItems();
            StrategicAi.RunTurn(this);
        }

        private void HuntOneDogForMeat()
        {
            Dog dog = CurrentLocation.Characters
                .OfType<Dog>()
                .FirstOrDefault(candidate => !candidate.IsDead);
            if (dog == null)
                return;

            Item[] existingMeat = CurrentLocation.Items
                .Where(item => item.ID == "item_meat")
                .ToArray();
            dog.Die();
            Item meat = CurrentLocation.Items
                .FirstOrDefault(item => item.ID == "item_meat" &&
                    !existingMeat.Contains(item));
            if (meat == null)
                return;

            string destination = "on the ground";
            if (TryStoreInGroup(meat))
            {
                CurrentLocation.Items.Remove(meat);
                destination = "in the travelling group";
            }
            else if (LocalOpportunities.StoreItemInCamp(CurrentLocation, meat))
            {
                CurrentLocation.Items.Remove(meat);
                destination = $"in a room at {CurrentLocation.Title}";
            }
            else if (LocalOpportunities.TryReplaceCargo(
                this, meat, out Item replaced, out Character carrier))
            {
                CurrentLocation.Items.Remove(meat);
                carrier.Items.Add(meat);
                CurrentLocation.Items.Add(replaced);
                destination = $"with {carrier.Name}, replacing {replaced.ID}";
            }
            else
            {
                var forced = Player.Group
                    .SelectMany(character => character.Items.Select(item =>
                        new { Character = character, Item = item }))
                    .Where(entry => !AiItemPool.IsWaterContainer(entry.Item.Type) &&
                        entry.Character.Weapon != entry.Item &&
                        entry.Character.Protection != entry.Item)
                    .OrderBy(entry => entry.Item.Type.TradeValue)
                    .FirstOrDefault();
                if (forced != null)
                {
                    forced.Character.Items.Remove(forced.Item);
                    CurrentLocation.Items.Add(forced.Item);
                    CurrentLocation.Items.Remove(meat);
                    forced.Character.Items.Add(meat);
                    destination = $"with {forced.Character.Name}, replacing {forced.Item.ID}";
                }
            }

            AiTelemetry.Report(Player,
                $"killed one dog for free at {CurrentLocation.Title} and stored item_meat {destination}");
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
        internal Character FindRecruitAt(
            Location location,
            bool requireAffordable,
            bool allowGeneratedPayment)
        {
            IEnumerable<Character> candidates = GetHireableCandidates(location);
            if (requireAffordable)
                candidates = candidates.Where(candidate =>
                    CanFundRecruit(candidate, allowGeneratedPayment));
            return candidates
                .OrderBy(candidate => candidate.HireItems
                    .Select(type => type.TradeValue)
                    .DefaultIfEmpty(0)
                    .Min())
                .ThenBy(candidate => candidate.Name)
                .FirstOrDefault();
        }
        internal bool RecruitmentSupplyCost(
            Character recruit,
            bool allowGeneratedPayment,
            out int food,
            out int water)
        {
            food = 0;
            water = 0;
            if (recruit == null)
                return false;

            Item exactPayment = recruit.HireItems
                .Select(type => Player.Character.Items.Find(type))
                .FirstOrDefault(item => item != null);
            if (exactPayment != null)
            {
                food = exactPayment.FoodValue;
                water = exactPayment.WaterValue;
                return true;
            }
            if (recruit.HireItems.Count == 0)
                return true;
            if (!allowGeneratedPayment ||
                !TryPlanRecruitmentPayment(recruit, out _, out List<RecruitmentAsset> assets))
                return false;

            food = assets.Where(asset => asset.Portable).Sum(asset => asset.Item.FoodValue);
            water = assets.Where(asset => asset.Portable).Sum(asset => asset.Item.WaterValue);
            return true;
        }
        internal bool CanRecruit(bool allowGeneratedPayment)
        {
            Character candidate = GetHireableNpc(
                requireAffordable: true,
                allowGeneratedPayment: allowGeneratedPayment);
            if (candidate == null || Player.Group.Count >= Group.MAX_PEOPLE)
                return false;
            return true;
        }
        internal bool ShouldReserveSettlerPayment => CurrentLocation.IsCity && HasSettlementPlan &&
            Player.Group.Count == 1 && GetHireableCandidates().Any(candidate =>
                CanFundRecruit(candidate, allowGeneratedPayment: true));
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

        internal Character ReleaseFollowerForSurvival()
        {
            Character follower = Player.Group
                .Where(character => character != Player.Character)
                .OrderBy(character => character.Health)
                .ThenBy(character => character.Water)
                .ThenBy(character => character.Food)
                .FirstOrDefault();
            follower?.Dismiss();
            return follower;
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
                ? ReinforcementTask.SustainableGarrisonTarget(
                    CurrentLocation, criticalGarrisonTarget)
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
            if (Player.Group.Count > 1)
                return Player.Group[1];

            return CanHireNpc() ? HireNpc(allowGeneratedPayment: CurrentLocation.IsCity) : null;
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
            DefenseEstimate defense = DefenseIntelligence.Estimate(this, location);
            int attackGroupSize = AttackTask.RequiredAttackGroupSize(this, location, policy);
            AiTelemetry.Report(Player,
                $"started attack plan for {location.Title}: " +
                $"{(defense.BasedOnContact ? "observed" : "inferred")} about " +
                $"{defense.ExpectedDefenders} defender{(defense.ExpectedDefenders == 1 ? "" : "s")}, " +
                $"preparing {attackGroupSize} attackers with {policy.AttackPlanTurns} days available");
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
            AiPolicy policy,
            bool madeProgress = false)
        {
            failedAttackCamp = location;
            failedAttackUntilDay = RootGame.World.Day +
                (madeProgress ? 3 : policy.FailedAttackMemoryTurns);
            failedAttackGroupSize = groupSize;
            failedAttackerStrength = attackerStrength;
            failedDefenderStrength = defenderStrength;
            AiTelemetry.Report(Player, madeProgress
                ? $"learned the reduced defense at {location.Title} and may return after recovering"
                : $"will reconsider {location.Title} only after recruiting, re-equipping, or weakening its defenders");
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
            if (!LocalOpportunities.StoreItemInCamp(CurrentLocation, trap))
            {
                ItemPool.Insert(trap);
                return false;
            }
            return true;
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
                    if (LocalOpportunities.StoreItemInCamp(CurrentLocation, item))
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
            bool reserveLastPortableTool = existingTool == null &&
                ExpansionTask.CanBootstrapCamp(this, CurrentLocation) &&
                ExpansionTask.ShouldReserveProductionTool(this);
            Item trap = existingTool == null || preferProductionUpgrade
                ? ItemPool.HasTrap(GetAvailableProducts(CurrentLocation))
                    ? ItemPool.GetBestTrap(GetAvailableProducts(CurrentLocation))
                    : existingTool == null && !reserveLastPortableTool
                        ? TakeCompatibleGroupProduction(CurrentLocation)
                        : null
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
                    if (!LocalOpportunities.StoreItemInCamp(CurrentLocation, trap))
                        npc.Items.Add(trap);
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
        Character[] GetHireableCandidates() => GetHireableCandidates(CurrentLocation);

        Character[] GetHireableCandidates(Location location)
        {
            Character[] available = location.Characters
                .Where(character => !character.IsDead && !character.IsHired && character.IsHuman && !character.IsTrader)
                .ToArray();
            if (available.Length == 0)
                return available;

            (int minimum, int maximum) = RootGame.World.Difficulty switch
            {
                0 => (0, 40),
                1 => (20, 60),
                _ => (40, int.MaxValue)
            };
            Character[] preferred = available
                .Where(character => character.Experience >= minimum && character.Experience <= maximum)
                .ToArray();
            return preferred.Length > 0 ? preferred : available;
        }

        protected Character GetHireableNpc(
            bool requireAffordable = false,
            bool allowGeneratedPayment = false)
        {
            Character[] candidates = GetHireableCandidates();
            if (requireAffordable)
                candidates = candidates
                    .Where(candidate => CanFundRecruit(candidate, allowGeneratedPayment))
                    .ToArray();
            if (candidates.Length == 0)
                return null;
            return candidates[Burntime.Platform.Math.Random.Next(candidates.Length)];
        }

        /// <summary>
        /// Hire NPC and add to group.
        /// </summary>
        /// <returns>hired NPC</returns>
        protected Character HireNpc(bool allowGeneratedPayment = true)
        {
            Character ch = GetHireableNpc(
                requireAffordable: true,
                allowGeneratedPayment: allowGeneratedPayment);
            if (ch == null || Player.Group.Count >= Group.MAX_PEOPLE)
                return null;

            if (ch.HireItems.Count > 0)
            {
                Item payment = ch.HireItems
                    .Select(type => Player.Character.Items.Find(type))
                    .FirstOrDefault(item => item != null);

                if (payment == null && allowGeneratedPayment)
                {
                    if (!TryPlanRecruitmentPayment(ch, out ItemType paymentType,
                        out List<RecruitmentAsset> paymentAssets))
                        return null;

                    foreach (RecruitmentAsset asset in paymentAssets)
                    {
                        if (asset.FromPool)
                            ItemPool.TryConsumeConstructionMaterial(asset.Item.ID);
                        else
                            asset.Owner.Remove(asset.Item);
                    }
                    Player.Character.Items.Add(paymentType.Generate());
                    AiTelemetry.Report(Player,
                        $"funded recruitment of {ch.Name} with " +
                        $"{string.Join(", ", paymentAssets.Select(asset => asset.Item.ID))} " +
                        $"(required value {paymentType.TradeValue}, paid " +
                        $"{paymentAssets.Sum(asset => asset.Item.TradeValue)})");
                }
                else if (payment == null)
                {
                    return null;
                }
                else
                {
                    AiTelemetry.Report(Player,
                        $"funded recruitment of {ch.Name} with requested {payment.ID} " +
                        $"(required value {payment.TradeValue}, paid {payment.TradeValue})");
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

        bool TryPlanRecruitmentPayment(
            Character recruit,
            out ItemType paymentType,
            out List<RecruitmentAsset> paymentAssets)
        {
            paymentType = recruit.HireItems
                .OrderBy(type => type.TradeValue)
                .FirstOrDefault();
            paymentAssets = new List<RecruitmentAsset>();
            if (paymentType == null)
                return true;

            int remainingFood = TradeTask.PortableFoodSupply(this);
            int remainingWaterCapacity = TradeTask.PortableWaterCapacity(this);
            List<RecruitmentAsset> candidates = Player.Group
                .SelectMany(character => character.Items
                    .Select(item => new RecruitmentAsset(
                        character.Items, item, Portable: true, FromPool: false)))
                .ToList();
            candidates.AddRange(ItemPool.GetContents()
                .Where(entry => AiItemPool.IsConstructionMaterial(entry.Type.ID))
                .SelectMany(entry => Enumerable.Range(0, entry.Count)
                    .Select(_ => new RecruitmentAsset(
                        null, entry.Type.Generate(), Portable: false, FromPool: true))));
            foreach (Location camp in RootGame.World.Locations.Where(location =>
                location.Player == Player))
            {
                candidates.AddRange(camp.Rooms
                    .SelectMany(room => room.Items
                        .Select(item => new RecruitmentAsset(
                            room.Items, item, Portable: false, FromPool: false))));
            }

            float paidValue = 0;
            foreach (RecruitmentAsset asset in candidates
                .Where(asset => CanUseForRecruitmentPayment(asset.Item,
                    remainingFood, remainingWaterCapacity, asset.Portable))
                .OrderBy(asset => asset.Item.TradeValue)
                .ThenBy(asset => TradeTask.SalePriority(asset.Item)))
            {
                if (!CanUseForRecruitmentPayment(asset.Item,
                    remainingFood, remainingWaterCapacity, asset.Portable))
                    continue;
                paymentAssets.Add(asset);
                paidValue += asset.Item.TradeValue;
                if (asset.Portable)
                {
                    remainingFood -= asset.Item.FoodValue;
                    remainingWaterCapacity -= AiItemPool.WaterContainerCapacity(asset.Item.Type);
                }
                if (paidValue >= paymentType.TradeValue)
                    break;
            }

            if (paidValue < paymentType.TradeValue)
            {
                paymentAssets.Clear();
                return false;
            }

            // Reduce greedy overpayment with the same high-to-low removal pass
            // used by barter offers.
            foreach (RecruitmentAsset asset in paymentAssets
                .OrderByDescending(asset => asset.Item.TradeValue)
                .ToArray())
            {
                if (paymentAssets.Count <= 1 ||
                    paidValue - asset.Item.TradeValue < paymentType.TradeValue)
                    continue;
                paymentAssets.Remove(asset);
                paidValue -= asset.Item.TradeValue;
            }

            // Character.Hire consumes the formal payment from the leader. Ensure
            // the value-backed bundle also frees a leader slot when necessary.
            if (Player.Character.Items.IsFull &&
                !paymentAssets.Any(asset => asset.Owner == Player.Character.Items))
            {
                RecruitmentAsset leaderAsset = Player.Character.Items
                    .Where(item => CanUseForRecruitmentPayment(
                        item, remainingFood, remainingWaterCapacity, portable: true))
                    .OrderBy(item => item.TradeValue)
                    .Select(item => new RecruitmentAsset(
                        Player.Character.Items, item, Portable: true, FromPool: false))
                    .FirstOrDefault();
                if (leaderAsset.Item == null)
                {
                    paymentAssets.Clear();
                    return false;
                }
                paymentAssets.Add(leaderAsset);
            }
            return true;
        }

        bool CanFundRecruit(Character recruit, bool allowGeneratedPayment)
        {
            if (recruit.HireItems.Count == 0 ||
                recruit.HireItems.Any(type => Player.Character.Items.Find(type) != null))
                return true;
            return allowGeneratedPayment && TryPlanRecruitmentPayment(recruit, out _, out _);
        }

        bool CanUseForRecruitmentPayment(
            Item item,
            int remainingFood,
            int remainingWaterCapacity,
            bool portable)
        {
            if (Player.Group.Any(character => character.Weapon == item || character.Protection == item))
                return false;
            if (item.Type.Production != null || TradeTask.IsPump(item) ||
                TradeTask.ConstructionMaterialPriority(this, item.ID) > 0 &&
                    TradeTask.CompletesUsefulRecipe(this, item.ID))
                return false;
            if (portable && item.FoodValue > 0 &&
                remainingFood - item.FoodValue < Player.Group.Count * 3)
                return false;
            if (portable && AiItemPool.IsWaterContainer(item.Type) &&
                remainingWaterCapacity - AiItemPool.WaterContainerCapacity(item.Type) <
                    TradeTask.DesiredWaterContainerCapacity(this))
                return false;
            if (!portable && AiItemPool.IsWaterContainer(item.Type))
                return false;
            if (AiItemPool.IsHazardProtection(item.Type) &&
                TradeTask.NeedsDangerProtection(this, item.Type))
                return false;
            return item.ID != "item_advice" && item.TradeValue > 0;
        }

        readonly record struct RecruitmentAsset(
            IItemCollection? Owner,
            Item Item,
            bool Portable,
            bool FromPool);

        #endregion
    }
}
