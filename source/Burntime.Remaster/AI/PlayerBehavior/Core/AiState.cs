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
        // Serialized by v1.0.4. Keep these unused members so the legacy settings
        // contract remains explicit and old save data continues to bind cleanly.
        public int MinInterval;
        public int MaxInterval;

        /// <summary>
        /// Maximum number of camps in advance of human players.
        /// </summary>
        public int MaxAdvance;

        /// <summary>
        /// AI policy profile: 0 = easy, 1 = normal, 2 = hard.
        /// The default value intentionally maps old saves without this field to easy.
        /// </summary>
        [System.Runtime.Serialization.OptionalField]
        public int Difficulty;
    }
    #endregion

    /// <summary>
    /// AI processing StateObject.
    /// Save compatibility policy: preserve members present in v1.0.4. Prefer
    /// <see cref="NonSerializedAttribute"/> for new tactical memory and rebuild it
    /// in <see cref="AfterResolving"/> or <see cref="InitAfterLoad"/>. A new value
    /// may be serialized only when reloading must not reset gameplay progress; it
    /// must then use <see cref="System.Runtime.Serialization.OptionalFieldAttribute"/>
    /// so v1.0.4 saves deserialize with the default value.
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
        // Legacy pre-v1.0.4 serialization field. The former camp interval is no
        // longer part of strategic expansion, but the field remains for saves.
        protected int wait;
        protected StateLink<AiItemPool> itemPool;
        [NonSerialized]
        protected StateLink<Player> retaliatingAgainst;
        [NonSerialized]
        protected StateLink<Location> recentlyContestedCamp;
        [NonSerialized]
        protected int retaliationUntilDay;
        [NonSerialized]
        protected int contestedUntilDay;
        // A post-capture pause is tactical runtime state, not general camp pacing.
        [NonSerialized]
        int postCapturePauseTurns;
        // Rebuilt for a persisted hostile target after loading.
        [NonSerialized]
        protected int attackPlanUntilDay;
        // A failed city recruitment means hostile expansion is not currently
        // feasible. Pause all new attack plans while ordinary economy and
        // neutral expansion continue, instead of rotating through enemy camps.
        [NonSerialized]
        bool attackRecruitmentDeferred;
        [NonSerialized]
        int attackRecruitmentGroupSize;
        [NonSerialized]
        int attackRecruitmentDeferredUntilDay;
        // Preserve the failed campaign long enough for the next planning pass
        // to relocate to a genuinely different frontier. DeferAttackPlan clears
        // StrategicTarget, so that target cannot carry this runtime intent.
        [NonSerialized]
        Location? recruitmentFailureTheaterAnchor;
        // Persist the use of configurable material assistance so saving and
        // reloading cannot refresh the faction's grant allowance.
        [System.Runtime.Serialization.OptionalField]
        int slumpMaterialGrantsUsed;
        // Runtime-only intent marker. It is restored from a neutral strategic
        // target after state links have been resolved on load.
        [NonSerialized]
        bool strategicTargetWasNeutral;
        // A persistent wait for the same target and reason means the current
        // plan is not making progress. This is runtime-only: loading a game
        // gives the plan a fresh chance under the then-current world state.
        [NonSerialized]
        Location? stalledWaitTarget;
        [NonSerialized]
        string? stalledWaitReason;
        [NonSerialized]
        int consecutiveStalledWaits;
        // Count stationary decisions independently of their target and score. A
        // blocked plan can otherwise rotate targets forever without making any
        // progress at the current location.
        [NonSerialized]
        int consecutiveNoProgressWaits;
        [NonSerialized]
        Location? noProgressLocation;
        // Unlike the stationary streak, this survives travel. Moving to another
        // camp is not strategic recovery unless a concrete action follows.
        [NonSerialized]
        int consecutiveStrategicNoProgressWaits;
        [NonSerialized]
        Location? previousExploratoryCamp;
        // Runtime-only multi-leg movement intent. Intermediate waypoints must not
        // cause ordinary trading or expansion to replace an unfinished escape.
        [NonSerialized]
        Location? committedJourneyDestination;
        [NonSerialized]
        string? committedJourneyReason;
        [NonSerialized]
        protected StateLink<Location> failedAttackCamp;
        [NonSerialized]
        protected int failedAttackUntilDay;
        [NonSerialized]
        protected int failedAttackGroupSize;
        [NonSerialized]
        protected float failedAttackerStrength;
        [NonSerialized]
        protected float failedDefenderStrength;
        [NonSerialized]
        protected StateLink<Location> lastChanceAttackTarget;
        // A fired follower remains available in the world, but this faction
        // must not turn demobilization into a repeated hire-and-fire expense.
        [NonSerialized]
        HashSet<Character>? demobilizedFollowers;
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
        /// Empire-wide strategic equipment and construction reserve.
        /// </summary>
        internal AiItemPool Reserve
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

            mode = Mode.None;
            wait = 0;

            itemPool = container.Create<AiItemPool>();
        }

        protected override void AfterResolving()
        {
            base.AfterResolving();
            strategicTargetWasNeutral = headedLocation != null &&
                headedLocation.Object?.Player == null;
        }

        internal void InitAfterLoad()
        {
            if (HasAttackPlan)
                attackPlanUntilDay = RootGame.World.Day +
                    AiPolicy.ForDifficulty(Difficulty).AttackPlanTurns;
            CampManagement.NormalizeLoadedGarrisonBelongings(this);
        }
        #endregion

        /// <summary>
        /// Process AI player turn.
        /// </summary>
        public void Turn()
        {
            int discardedReserveItems = Reserve.EnforceCaps();
            if (discardedReserveItems > 0)
                AiTelemetry.Report(Player,
                    $"discarded {discardedReserveItems} excess shared reserve items above the per-type cap");
            HuntOneDogForMeat();
            ManageLocalItems();
            AiTurnController.RunTurn(this);
        }

        private void HuntOneDogForMeat()
        {
            if (CurrentLocation.IsCity ||
                Trading.PortableFoodSupply(this) >= Trading.DesiredPortableFood(this))
                return;

            Dog dog = CurrentLocation.Characters
                .OfType<Dog>()
                .FirstOrDefault(candidate => !candidate.IsDead);
            if (dog == null)
                return;

            ItemType meatType = RootGame.ItemTypes["item_meat"];
            int foodCapacity = Player.Group.Sum(character => character.MaxFood - character.Food);
            bool canConsume = foodCapacity >= meatType.FoodValue;
            bool canCarry = Player.Group.Any(character =>
                GroupInventory.CanCarryCargo(this, character, meatType));
            bool canStoreAtOwnedCamp = CurrentLocation.Player == Player &&
                CampManagement.CanStoreItemInCamp(CurrentLocation, meatType);
            if (!canConsume && !canCarry && !canStoreAtOwnedCamp)
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

            string destination;
            if (canConsume)
            {
                Player.Group.Eat(null, meat.FoodValue);
                CurrentLocation.Items.Remove(meat);
                destination = "immediately consumed by the travelling group";
            }
            else if (TryStoreInGroup(meat))
            {
                CurrentLocation.Items.Remove(meat);
                destination = "in the travelling group";
            }
            else if (canStoreAtOwnedCamp && CampManagement.StoreItemInCamp(CurrentLocation, meat))
            {
                CurrentLocation.Items.Remove(meat);
                destination = $"in a room at {CurrentLocation.Title}";
            }
            else
            {
                // The preflight checks above should keep this unreachable. If the
                // state changes while the dog dies, do not create ground overflow.
                CurrentLocation.Items.Remove(meat);
                return;
            }

            AiTelemetry.Report(Player,
                $"killed one dog for free at {CurrentLocation.Title}; item_meat was {destination}");
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
        // The serialized itemPool field name is retained for pre-rename save compatibility.
        internal AiSettings Configuration => settings;
        internal int Difficulty => settings.Difficulty;
        internal int SlumpMaterialGrantsUsed
        {
            get => slumpMaterialGrantsUsed;
            set => slumpMaterialGrantsUsed = value;
        }
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
        internal Location? LastChanceAttackTarget
        {
            get => lastChanceAttackTarget != null ? lastChanceAttackTarget.Object : null;
            set => lastChanceAttackTarget = value;
        }
        internal int PostCapturePauseTurns
        {
            get => postCapturePauseTurns;
            set => postCapturePauseTurns = value;
        }

        internal bool CancelStalledStrategicWait(AiDecision decision, out int waitTurns)
        {
            waitTurns = 0;
            if (decision.Action != AiAction.Wait || decision.Target == null)
            {
                ResetStalledStrategicWait();
                return false;
            }

            if (stalledWaitTarget == decision.Target &&
                stalledWaitReason == decision.Reason)
            {
                consecutiveStalledWaits++;
            }
            else
            {
                stalledWaitTarget = decision.Target;
                stalledWaitReason = decision.Reason;
                consecutiveStalledWaits = 1;
            }

            const int maximumConsecutiveWaits = 5;
            if (consecutiveStalledWaits < maximumConsecutiveWaits)
                return false;

            waitTurns = consecutiveStalledWaits;
            StrategicTarget = null;
            ResetStalledStrategicWait();
            return true;
        }

        void ResetStalledStrategicWait()
        {
            stalledWaitTarget = null;
            stalledWaitReason = null;
            consecutiveStalledWaits = 0;
        }

        internal int StationaryRelocationTier(
            AiDecision decision,
            out int stationaryWaits,
            out int strategicWaits)
        {
            stationaryWaits = consecutiveNoProgressWaits;
            strategicWaits = consecutiveStrategicNoProgressWaits;
            if (decision.Reason == "post-capture pause")
            {
                ResetStationaryWaits();
                return 0;
            }
            if (decision.Action != AiAction.Wait)
            {
                ResetStationaryWaits();
                if (decision.Action != AiAction.Travel)
                    consecutiveStrategicNoProgressWaits = 0;
                return 0;
            }

            if (noProgressLocation != CurrentLocation)
            {
                noProgressLocation = CurrentLocation;
                consecutiveNoProgressWaits = 0;
            }

            stationaryWaits = ++consecutiveNoProgressWaits;
            strategicWaits = ++consecutiveStrategicNoProgressWaits;
            if (strategicWaits >= 6 && strategicWaits % 3 == 0)
                return 2;
            if (stationaryWaits == 3)
                return 1;
            return 0;
        }

        internal void ResetStationaryWaits()
        {
            consecutiveNoProgressWaits = 0;
            noProgressLocation = null;
        }

        internal void ResetNonProgressWatchdog()
        {
            ResetStationaryWaits();
            consecutiveStrategicNoProgressWaits = 0;
            previousExploratoryCamp = null;
        }

        internal Location? PreviousExploratoryCamp => previousExploratoryCamp;

        internal Location? CommittedJourneyDestination => committedJourneyDestination;

        internal string? CommittedJourneyReason => committedJourneyReason;

        internal void CommitJourney(Location destination, string reason)
        {
            committedJourneyDestination = destination;
            committedJourneyReason = reason;
        }

        internal void ClearCommittedJourney()
        {
            committedJourneyDestination = null;
            committedJourneyReason = null;
        }

        internal void MarkExploratoryRelocation(Location origin)
        {
            previousExploratoryCamp = origin;
        }

        internal bool CanClaim(Location location) => CanCreateCamp(location);
        internal bool CanStationCamp() => Player.Group.Count > 1 || CanRecruit(allowGeneratedPayment: CurrentLocation.IsCity);
        internal bool HasHireableNpc() => CanHireNpc();
        internal sealed record RecruitmentPlan(
            Character Recruit,
            bool IsFunded,
            int FoodCost,
            int WaterCost);

        internal RecruitmentPlan FindRecruitAt(
            Location location,
            bool requireAffordable,
            bool allowGeneratedPayment)
        {
            IEnumerable<RecruitmentPlan> candidates = GetHireableCandidates(location)
                .Select(candidate => CreateRecruitmentPlan(candidate, allowGeneratedPayment));
            if (requireAffordable)
                candidates = candidates.Where(candidate => candidate.IsFunded);
            return candidates
                .OrderBy(candidate => candidate.Recruit.HireItems
                    .Select(type => type.TradeValue)
                    .DefaultIfEmpty(0)
                    .Min())
                .ThenBy(candidate => candidate.Recruit.Name)
                .FirstOrDefault();
        }
        internal bool RecruitmentSupplyCost(
            RecruitmentPlan plan,
            out int food,
            out int water)
        {
            if (plan == null || !plan.IsFunded)
            {
                food = 0;
                water = 0;
                return false;
            }

            food = plan.FoodCost;
            water = plan.WaterCost;
            return true;
        }

        RecruitmentPlan CreateRecruitmentPlan(Character recruit, bool allowGeneratedPayment)
        {
            if (OwnedCampCount == 0 && Player.Group.Count == 1)
                return new RecruitmentPlan(recruit, IsFunded: true, 0, 0);

            Item exactPayment = recruit.HireItems
                .Select(type => Player.Character.Items.Find(type))
                .FirstOrDefault(item => item != null);
            if (exactPayment != null)
                return new RecruitmentPlan(
                    recruit,
                    IsFunded: true,
                    FoodCost: exactPayment.FoodValue,
                    WaterCost: exactPayment.WaterValue);
            if (recruit.HireItems.Count == 0)
                return new RecruitmentPlan(recruit, IsFunded: true, 0, 0);
            if (!allowGeneratedPayment ||
                !TryPlanRecruitmentPayment(recruit, out _, out List<RecruitmentAsset> assets))
                return new RecruitmentPlan(recruit, IsFunded: false, 0, 0);

            return new RecruitmentPlan(
                recruit,
                IsFunded: true,
                FoodCost: assets.Where(asset => asset.Portable).Sum(asset => asset.Item.FoodValue),
                WaterCost: assets.Where(asset => asset.Portable).Sum(asset => asset.Item.WaterValue));
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
            if (!Reserve.HasTrap(products))
                return false;

            bool HasCompatibleProduction(Item item) => item.Type.Production != null &&
                products.Contains(item.Type.Production.Produce.ID);
            return !CurrentLocation.Rooms.SelectMany(room => room.Items).Any(HasCompatibleProduction) &&
                !CurrentLocation.CampNPC.SelectMany(character => character.Items).Any(HasCompatibleProduction);
        }

        internal Character Recruit(
            bool allowGeneratedPayment,
            Character plannedRecruit = null)
        {
            return plannedRecruit == null
                ? HireNpc(allowGeneratedPayment)
                : HireNpc(plannedRecruit, allowGeneratedPayment);
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

        internal Character DismissSurplusFollower()
        {
            Character follower = Player.Group
                .Where(character => character != Player.Character)
                .OrderBy(character => character.Class switch
                {
                    CharClass.Doctor => 3,
                    CharClass.Technician => 3,
                    CharClass.Mercenary => 2,
                    _ => 1
                })
                .ThenBy(character => character.AttackValue + character.DefenseValue)
                .ThenBy(character => character.Health)
                .FirstOrDefault();
            if (follower != null)
                (demobilizedFollowers ??= new()).Add(follower);
            follower?.Dismiss();
            return follower;
        }

        internal Character StationSurplusFollower()
        {
            if (!IsHome || Player.Group.Count <= 1)
                return null;
            int guards = CampEconomy.LivingGuardCount(CurrentLocation, Player);
            if (!ReinforcementPlanning.CanSupportAdditionalGuard(this, CurrentLocation, guards))
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
            if (npc == null || !PreserveTravelSuppliesBeforeStationing(npc))
                return null;
            JoinCamp(npc);
            return npc;
        }

        bool PreserveTravelSuppliesBeforeStationing(Character follower)
        {
            const int minimumTravelDays = 5;
            Character[] remaining = Player.Group
                .Where(character => character != follower)
                .ToArray();
            if (remaining.Length == 0)
                return false;

            int LowestFoodDays() => Group.GetLowestAfterDistribution(
                remaining.Select(character => character.Food).ToArray(),
                remaining.SelectMany(character => character.Items).Sum(item => item.FoodValue));
            int WaterContainerCapacity() => remaining
                .SelectMany(character => character.Items)
                .Sum(item => AiItemPool.WaterContainerCapacity(item.Type));

            foreach (Item food in follower.Items
                .Where(item => item.FoodValue > 0)
                .OrderByDescending(item => item.FoodValue)
                .ToArray())
            {
                if (LowestFoodDays() >= minimumTravelDays)
                    break;
                if (!MoveSurvivalItemFromFollower(follower, remaining, food))
                    break;
            }

            int desiredWaterCapacity = remaining.Length * 3;
            foreach (Item container in follower.Items
                .Where(item => AiItemPool.IsWaterContainer(item.Type))
                .OrderByDescending(item => AiItemPool.WaterContainerCapacity(item.Type))
                .ToArray())
            {
                if (WaterContainerCapacity() >= desiredWaterCapacity)
                    break;
                if (!MoveSurvivalItemFromFollower(follower, remaining, container))
                    break;
            }

            bool canBuildFoodReserve =
                CampEconomy.FoodSurplusPerDay(CurrentLocation) > remaining.Length;
            bool canBuildWaterReserve =
                CampEconomy.WaterSurplusPerDay(CurrentLocation) > remaining.Length;
            return (LowestFoodDays() >= minimumTravelDays || canBuildFoodReserve) &&
                (WaterContainerCapacity() >= desiredWaterCapacity || canBuildWaterReserve);
        }

        bool MoveSurvivalItemFromFollower(
            Character follower,
            Character[] remaining,
            Item item)
        {
            Character carrier = remaining.FirstOrDefault(character => !character.Items.IsFull);
            if (carrier == null)
            {
                var replaceable = remaining
                    .SelectMany(character => character.Items.Select(candidate =>
                        (Character: character, Item: candidate)))
                    .Where(entry => entry.Item.FoodValue == 0 &&
                        !AiItemPool.IsWaterContainer(entry.Item.Type) &&
                        entry.Item != entry.Character.Weapon &&
                        entry.Item != entry.Character.Protection &&
                        entry.Item.Type.Production == null &&
                        !Trading.IsPump(entry.Item))
                    .OrderBy(entry => entry.Item.TradeValue)
                    .FirstOrDefault();
                if (replaceable.Item == null)
                    return false;

                replaceable.Character.Items.Remove(replaceable.Item);
                if (!CampManagement.StoreItemInCamp(CurrentLocation, replaceable.Item))
                {
                    replaceable.Character.Items.Add(replaceable.Item);
                    return false;
                }
                carrier = replaceable.Character;
            }

            follower.Items.Remove(item);
            if (carrier.Items.Add(item))
                return true;
            follower.Items.Add(item);
            return false;
        }

        internal Character StationTradeFollower()
        {
            if (!IsHome || Player.Group.Count <= 2)
                return null;
            int guards = CampEconomy.LivingGuardCount(CurrentLocation, Player);
            int garrisonLimit = AiPolicy.ForDifficulty(Difficulty).CriticalGarrisonTarget;
            if (guards >= garrisonLimit ||
                !ReinforcementPlanning.CanSupportAdditionalGuard(this, CurrentLocation, guards))
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
            int minimumGuards = ReinforcementPlanning.IsCriticalCamp(this, CurrentLocation)
                ? ReinforcementPlanning.SustainableGarrisonTarget(
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
        }

        internal void MarkRecentlyCaptured(Location location, AiPolicy policy)
        {
            recentlyContestedCamp = location;
            contestedUntilDay = RootGame.World.Day + policy.ContestedCampMemoryTurns;
            ClearFailedAttack(location);
            postCapturePauseTurns = System.Math.Max(
                postCapturePauseTurns, policy.AttackCooldownTurns);
        }

        internal void StartAttackPlan(Location location, AiPolicy policy)
        {
            if (StrategicTarget == location && attackPlanUntilDay > 0)
                return;
            headedLocation = location;
            strategicTargetWasNeutral = false;
            attackPlanUntilDay = RootGame.World.Day + policy.AttackPlanTurns;
            DefenseEstimate defense = DefenseIntelligence.Estimate(this, location);
            int attackGroupSize = AttackPlanning.RequiredAttackGroupSize(this, location, policy);
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
            DeferAttackPlan(location, policy);
        }

        internal void DeferAttackPlan(Location location, AiPolicy policy)
        {
            TerritorialTargetDeferrals.DeferForTurns(
                this, location, policy.AttackPlanRetryDelay);
            StrategicTarget = null;
        }

        internal bool IsAttackRecruitmentDeferred(AiPolicy policy)
        {
            if (!attackRecruitmentDeferred)
                return false;

            // A failed city remains unproductive, but it must not suppress every
            // other hostile opportunity forever. Retry the wider strategic
            // picture after a short economic/relocation window.
            bool recruitmentReady = Player.Group.Count > attackRecruitmentGroupSize ||
                Current.IsCity && Recruitment.HasSafeLocalRecruit(this, policy) ||
                RootGame.World.Day >= attackRecruitmentDeferredUntilDay;
            if (recruitmentReady)
                attackRecruitmentDeferred = false;
            return attackRecruitmentDeferred;
        }

        internal void DeferAttacksForFailedCityRecruitment(
            Location location,
            AiPolicy policy)
        {
            recruitmentFailureTheaterAnchor = location;
            DeferAttackPlan(location, policy);
            attackRecruitmentDeferred = true;
            attackRecruitmentGroupSize = Player.Group.Count;
            attackRecruitmentDeferredUntilDay = RootGame.World.Day + 20;
        }

        internal Location? ConsumeRecruitmentFailureTheaterAnchor()
        {
            Location? location = recruitmentFailureTheaterAnchor;
            recruitmentFailureTheaterAnchor = null;
            return location;
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
                (madeProgress
                    ? policy.ProgressingAttackRetryTurns
                    : policy.FailedAttackMemoryTurns);
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
                Difficulty).ContestedCampMemoryTurns;

            if (attacker.Player.Type != PlayerType.Human)
                return;
            retaliatingAgainst = attacker.Player;
            retaliationUntilDay = RootGame.World.Day + AiPolicy.ForDifficulty(
                Difficulty).RetaliationTurns;
            AiTelemetry.Report(Player,
                $"will retaliate against {attacker.Player.Name} after the attack at {defender.Location.Title}");
        }

        internal bool ImproveCamp()
        {
            if (!IsHome || !Reserve.HasTrap(GetAvailableProducts(CurrentLocation)))
                return false;

            Item trap = Reserve.GetBestTrap(GetAvailableProducts(CurrentLocation));
            if (trap == null)
                return false;
            if (!CampManagement.StoreItemInCamp(CurrentLocation, trap))
            {
                Reserve.Insert(trap);
                return false;
            }
            return true;
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

                foreach (Player human in Game.World.Players.Where(candidate =>
                    candidate.Type == PlayerType.Human))
                {
                    int count = Game.World.Locations.Count(location =>
                        location.Player == human);
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
        private void ManageLocalItems()
        {
#warning after loading savegame this may be null, why?
            if (itemPool == null)
                itemPool = container.Create<AiItemPool>();

            CollectGroundItems();

            if (IsHome)
                StockCampWaterContainers();

            // Keep a camp stock when possible, but use it for the travelling
            // group when an expedition needs the capacity. A canteen covers one
            // traveller; a wineskin may cover two.
            EquipTravelWaterContainers();
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
            foreach (Item item in groundItems
                .OrderByDescending(item => item.FoodValue > 0)
                .ThenByDescending(item => item.FoodValue)
                .ThenByDescending(item => item.TradeValue))
            {
                if (AiItemPool.Accepts(item.Type) && Reserve.Insert(item))
                {
                    CurrentLocation.Items.Remove(item);
                }
                else if (Reserve.TryReserveConstructionMaterial(item))
                {
                    // Preserve the first copy even when the travel group deliberately
                    // kept only limited room for unexpected ground loot.
                    CurrentLocation.Items.Remove(item);
                }
                else if (TryStoreInGroup(item))
                {
                    CurrentLocation.Items.Remove(item);
                }
                else if (CargoManagement.TryReplaceCargo(
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
                    if (CampManagement.StoreItemInCamp(CurrentLocation, item))
                        CurrentLocation.Items.Remove(item);
                }
            }
        }

        private bool TryStoreInGroup(Item item)
        {
            Character carrier = GroupInventory.FindCargoCarrier(this, item);
            return carrier != null && carrier.Items.Add(item);
        }

        private static bool HasWaterContainer(Character character) =>
            character.Items.Any(item => AiItemPool.IsWaterContainer(item.Type));

        private void EquipTravelWaterContainers()
        {
            int required = Trading.DesiredWaterContainerCapacity(this);
            while (Trading.PortableWaterCapacity(this) < required)
            {
                Character carrier = Player.Group.FirstOrDefault(character => !character.Items.IsFull &&
                    !HasWaterContainer(character));
                if (carrier == null)
                    return;

                Item container = Reserve.HasWaterContainer()
                    ? Reserve.GetBestWaterContainer()
                    : IsHome ? TakeBestStoredWaterContainer() : null;
                if (container == null)
                    return;
                carrier.Items.Add(container);
            }
        }

        private void EquipWaterContainers(IEnumerable<Character> characters)
        {
            foreach (Character character in characters)
            {
                if (HasWaterContainer(character) || character.Items.IsFull)
                    continue;

                Item container = Reserve.HasWaterContainer()
                    ? Reserve.GetBestWaterContainer()
                    : null;
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

        private void StockCampWaterContainers()
        {
            while (Trading.CampWaterContainerCount(CurrentLocation) <
                Trading.DesiredCampWaterContainerCount(CurrentLocation) && Reserve.HasWaterContainer())
            {
                Item container = Reserve.TakeLeastWaterContainer();
                if (container == null || !CampManagement.StoreItemInCamp(CurrentLocation, container))
                {
                    if (container != null)
                        Reserve.Insert(container);
                    return;
                }
            }
        }
        #endregion

        #region protected camp management methods
        /// <summary>
        /// Create a camp at current location.
        /// </summary>
        /// <param name="npc">NPC to join camp</param>
        protected void JoinCamp(Character npc)
        {
            if (CurrentLocation.Danger != null && !npc.Items.IsFull)
            {
                Item protection = CurrentLocation.Danger.Type == "radiation"
                    ? Reserve.GetProtectionSuit()
                    : Reserve.GetGasMask();
                if (protection != null)
                {
                    npc.Items.Add(protection);
                    npc.Protection = protection;
                }
            }

            // join camp
            npc.JoinCamp();
            CampManagement.UnloadGarrisonBelongings(this, CurrentLocation, npc);

            // Add a real compatible production tool. A carried weapon such as a knife can
            // remain on the guard and serve both defense and maggot production.
            Item existingTool = CurrentLocation.Rooms.SelectMany(room => room.Items)
                .Concat(npc.Items)
                .FirstOrDefault(item => item.Type.Production != null &&
                    GetAvailableProducts(CurrentLocation).Contains(item.Type.Production.Produce.ID));
            bool preferProductionUpgrade = CampManagement.ShouldPreferProductionAtCamp(this, CurrentLocation) &&
                Reserve.HasHigherValueTrap(existingTool?.Type.Production?.Produce.TradeValue ?? -1,
                    GetAvailableProducts(CurrentLocation));
            bool reserveLastPortableTool = existingTool == null &&
                ExpansionPlanning.CanBootstrapCamp(this, CurrentLocation) &&
                ExpansionPlanning.ShouldReserveProductionTool(this);
            Item trap = existingTool == null || preferProductionUpgrade
                ? Reserve.HasTrap(GetAvailableProducts(CurrentLocation))
                    ? Reserve.GetBestTrap(GetAvailableProducts(CurrentLocation))
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
                    if (!CampManagement.StoreItemInCamp(CurrentLocation, trap))
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

            GroupInventory.MaintainLeaderRoleSlots(this);
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
            bool hasProductionTool = Reserve.HasTrap(GetAvailableProducts(location)) ||
                FindCompatibleGroupProduction(location) != null;
            if (!hasProductionTool && !ExpansionPlanning.CanBootstrapCamp(this, location))
                return false;

            // in case of hazards
            if (location.Danger != null)
            {
                if (location.Danger.Type == "gas")
                {
                    // no gas mask
                    if (!Reserve.HasGasMask())
                        return false;
                }
                else if (location.Danger.Type == "radiation")
                {
                    // no protection suit
                    if (!Reserve.HasProtectionSuit())
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
                .Where(character => !character.IsDead && !character.IsHired &&
                    character.IsHuman && !character.IsTrader &&
                    demobilizedFollowers?.Contains(character) != true)
                .ToArray();
            if (available.Length == 0)
                return available;

            AiPolicy policy = AiPolicy.ForDifficulty(Difficulty);
            (int minimum, int maximum) =
                (policy.MinimumRecruitExperience, policy.MaximumRecruitExperience);
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
            return HireNpc(ch, allowGeneratedPayment);
        }

        Character HireNpc(Character ch, bool allowGeneratedPayment)
        {
            bool freeFirstRecruit = OwnedCampCount == 0 && Player.Group.Count == 1;
            if (ch == null || Player.Group.Count >= Group.MAX_PEOPLE ||
                !GetHireableCandidates().Contains(ch) ||
                (!freeFirstRecruit && !CanFundRecruit(ch, allowGeneratedPayment)))
                return null;

            if (freeFirstRecruit)
            {
                AiTelemetry.Report(Player,
                    $"recruited first settler {ch.Name} for free");
            }
            else if (ch.HireItems.Count > 0)
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
                        asset.Owner.Remove(asset.Item);
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
            ch.Hire(Player, waivePayment: freeFirstRecruit);

            EquipWaterContainers(new[] { ch });

            // add weapon to npc
            if (Reserve.HasWeapon() && !ch.Items.IsFull)
            {
                // An unarmed new follower needs the tool now. Knives still serve food
                // production later when that follower is stationed at a camp.
                Item weapon = Reserve.GetBestWeapon(allowProductionTool: true);
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

            // Generated recruitment payments are backed only by surplus food.
            // Exact requested items carried by the leader are handled before this
            // method, so equipment and construction reserves never fund substitutes.
            int remainingFood = Trading.PortableFoodSupply(this);
            List<RecruitmentAsset> candidates = Player.Group
                .SelectMany(character => character.Items
                    .Where(item => item.FoodValue > 0)
                    .Select(item => new RecruitmentAsset(
                        character.Items, item, Portable: true)))
                .ToList();
            foreach (Location camp in RootGame.World.Locations.Where(location =>
                location.Player == Player))
            {
                candidates.AddRange(camp.Rooms
                    .SelectMany(room => room.Items
                        .Where(item => item.FoodValue > 0)
                        .Select(item => new RecruitmentAsset(
                            room.Items, item, Portable: false))));
            }

            float paidValue = 0;
            foreach (RecruitmentAsset asset in candidates
                .Where(asset => CanUseForRecruitmentPayment(asset.Item,
                    remainingFood, asset.Portable))
                .OrderBy(asset => asset.Item.TradeValue)
                .ThenBy(asset => Trading.SalePriority(asset.Item)))
            {
                if (!CanUseForRecruitmentPayment(asset.Item,
                    remainingFood, asset.Portable))
                    continue;
                paymentAssets.Add(asset);
                paidValue += asset.Item.TradeValue;
                if (asset.Portable)
                    remainingFood -= asset.Item.FoodValue;
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
                        item, remainingFood, portable: true))
                    .OrderBy(item => item.TradeValue)
                    .Select(item => new RecruitmentAsset(
                        Player.Character.Items, item, Portable: true))
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
            => CreateRecruitmentPlan(recruit, allowGeneratedPayment).IsFunded;

        bool CanUseForRecruitmentPayment(
            Item item,
            int remainingFood,
            bool portable)
        {
            if (item.FoodValue <= 0)
                return false;
            if (Player.Group.Any(character => character.Weapon == item || character.Protection == item))
                return false;
            if (portable &&
                remainingFood - item.FoodValue < Player.Group.Count * 3)
                return false;
            return item.TradeValue > 0;
        }

        readonly record struct RecruitmentAsset(
            IItemCollection Owner,
            Item Item,
            bool Portable);

        #endregion
    }
}
