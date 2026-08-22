using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class TradeTask
{
    internal static void TradeWithTrader(ClassicAiState state, Trader trader)
    {
        if (trader == null)
            return;

        bool firstExposureToday = EconomicSupport.RecordTraderExposure(state, trader);
        bool snakeTrapAvailable = trader.Items.Any(item => item.ID == "item_snake_trap");
        bool demandedSnakeTrapAvailable = snakeTrapAvailable && HasStrategicSnakeTrapNeed(state);
        if (demandedSnakeTrapAvailable)
            EconomicSupport.StartSnakeTrapCampaign(state);
        if (firstExposureToday && snakeTrapAvailable)
            AiTelemetry.Report(state.Player,
                $"encountered item_snake_trap with {trader.Name}" +
                (demandedSnakeTrapAvailable ? " for current or future camp production" : string.Empty));
        if (trader.Items.Count == 0)
            return;

        HashSet<Item> soldToTrader = new();
        int completed = 0;
        TradePlan nextPlan = CreateTradePlan(state, trader, soldToTrader);
        if (nextPlan != null)
        {
            int capacity = state.Player.Group.Sum(character => character.Items.MaxCount);
            int cargo = capacity - state.Player.Group.GetFreeSlotCount();
            float sellableValue = state.Player.Group.SelectMany(character => character.Items)
                .Where(item => CanSell(state, item))
                .Sum(item => item.TradeValue);
            string visit = state.Current.IsCity
                ? TradeTask.HasPreparedTradeCargo(state) ? "prepared city" : "incidental city"
                : "roaming";
            AiTelemetry.Report(state.Player,
                $"{visit} barter with {trader.Name}: cargo {cargo}/{capacity} slots, " +
                $"sellable value {sellableValue:0}");
        }

        for (int exchange = 0; exchange < 6; exchange++)
        {
            TradePlan plan = nextPlan;
            if (plan == null)
                break;

            for (int index = 0; index < plan.Offers.Count; index++)
            {
                TradeAsset offer = plan.Offers[index];
                if (!offer.FromPool)
                    continue;
                Item item = state.Pool.TakeForTrade(offer.Type) ??
                    throw new InvalidOperationException("planned pool trade asset is no longer available");
                plan.Offers[index] = new TradeAsset(null, item, true);
            }
            float offeredValue = plan.Offers.Sum(offer => offer.TradeValue);
            foreach (Item target in plan.Targets)
                trader.Items.Remove(target);
            foreach (TradeAsset offer in plan.Offers.Where(offer => !offer.FromPool))
            {
                Character owner = state.Player.Group
                    .FirstOrDefault(character => character.Items == offer.Owner);
                if (owner?.Weapon == offer.Item)
                    owner.Weapon = null;
                offer.Owner.Remove(offer.Item);
            }
            foreach (TradeAsset offer in plan.Offers)
                soldToTrader.Add(offer.Item!);

            PruneAndStoreTraderOffers(trader, plan.Offers.Select(offer => offer.Item!));

            foreach (Item target in plan.Targets)
            {
                if (AiItemPool.Accepts(target.Type))
                    state.Pool.Insert(target);
                else
                    state.Player.Group.First(character => !character.Items.IsFull).Items.Add(target);
            }

            completed++;
            if (plan.Targets.Any(target => target.ID == "item_snake_trap"))
                EconomicSupport.CompleteSnakeTrapCampaign(state);
            string action = plan.Targets.Any(target => IsStrategicPurchase(state, target))
                ? "traded"
                : "consolidated";
            float receivedValue = plan.Targets.Sum(target => target.TradeValue);
            AiTelemetry.Report(state.Player,
                $"{action} {string.Join(", ", plan.Offers.Select(offer => offer.ID))} for " +
                $"{string.Join(", ", plan.Targets.Select(target => target.ID))} with {trader.Name} " +
                $"(value {offeredValue:0} -> {receivedValue:0}, " +
                $"AI barter value x{TradeBenefit(state):0.0})");

            // Firearm parts are never shopping goals, but when normal value trading
            // happens to put both pieces together, convert them into the denser item.
            LocalOpportunities.ConstructPortableWeapon(state);
            nextPlan = CreateTradePlan(state, trader, soldToTrader);
        }

        if (completed > 0)
        {
            TradeTask.LastReportedTradeFailure.Remove(state.Player);
            return;
        }

        if (AiTelemetry.Sink != null &&
            trader.Items.Any(item => ShoppingPriority(state, item) > 0) &&
            state.Player.Group.SelectMany(character => character.Items).Any(item => CanSell(state, item)))
        {
            string signature = trader.Name;
            TradeFailureState failure = TradeTask.LastReportedTradeFailure.GetOrCreateValue(state.Player);
            if (failure.Signature != signature)
            {
                AiTelemetry.Report(state.Player,
                    $"could not complete a useful trade with {trader.Name}: insufficient safe offers or inventory space");
                failure.Signature = signature;
            }
        }
    }

    internal static void PruneAndStoreTraderOffers(Trader trader, IEnumerable<Item> offers)
    {
        Item[] offeredItems = offers.ToArray();
        if (trader.Items.MaxCount == ItemList.Infinite)
        {
            foreach (Item item in offeredItems)
                trader.Items.Add(item);
            return;
        }

        HashSet<Item> kept = trader.Items.Concat(offeredItems)
            .OrderByDescending(item => item.TradeValue)
            .ThenBy(item => item.Type.IsClass("useless"))
            .Take(trader.Items.MaxCount)
            .ToHashSet();
        foreach (Item item in trader.Items.Where(item => !kept.Contains(item)).ToArray())
            trader.Items.Remove(item);
        foreach (Item item in offeredItems.Where(kept.Contains))
            trader.Items.Add(item);
    }

    internal static bool CanPlanTrade(ClassicAiState state, Trader trader)
    {
        TradePlan plan = CreateTradePlan(state, trader);
        return plan != null;
    }

    internal static TradePlan CreateTradePlan(ClassicAiState state, Trader trader, ISet<Item> excludedTargets = null)
    {
        if (trader == null || trader.Items.Count == 0)
            return null;

        List<TradeAsset> temporaryPoolAssets = SnapshotSurplusPoolAssets(state);
        foreach (Item target in trader.Items
            .Where(item => item.TradeValue > 0 &&
                (excludedTargets == null || !excludedTargets.Contains(item)))
            .Select(item => new { Item = item, Priority = ShoppingPriority(state, item) })
            .Where(candidate => candidate.Priority > 0)
            .OrderByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Item.TradeValue)
            .Select(candidate => candidate.Item))
        {
            bool spendReserves =
                target.ID == "item_snake_trap" &&
                    HasStrategicSnakeTrapNeed(state) ||
                AdvancesOwnedMeatTrapRecipe(state, target.ID) ||
                IsEarlyRareProductionPurchase(state, target.ID);
            HashSet<string> temporaryPoolItemIds = temporaryPoolAssets
                .Select(asset => asset.ID)
                .ToHashSet();
            List<TradeAsset> exceptionalPoolAssets = target.ID == "item_snake_trap" && spendReserves
                ? state.Pool.SnapshotItemTypes()
                    .Where(type => TradeTask.ConstructionMaterials.Contains(type.ID) &&
                        !temporaryPoolItemIds.Contains(type.ID))
                    .Select(type => new TradeAsset(null, null, type, true))
                    .ToList()
                : new List<TradeAsset>();
            List<TradeAsset> allCandidates = state.Player.Group
                .SelectMany(character => character.Items
                    .Select(item => new TradeAsset(character.Items, item, false)))
                .Where(asset => CanSell(state, asset.Item!) ||
                    (spendReserves && (IsHighReturnLiquidReserve(asset.Item!) ||
                        TradeTask.ConstructionMaterials.Contains(asset.ID)) &&
                        !(AiItemPool.IsWaterContainer(asset.Type) &&
                            NeedsCriticalWaterWaypointUpgrade(state))))
                .Concat(temporaryPoolAssets)
                .Concat(exceptionalPoolAssets)
                .OrderBy(asset => IsFullWaterContainer(asset) ? 0 : 1)
                .ThenBy(asset => asset.TradeValue)
                .ThenBy(asset => asset.Item == null ? 3 : SalePriority(asset.Item))
                .ToList();
            List<TradeAsset> offers = new();
            float offeredValue = 0;
            int remainingFoodInventory = state.Player.Group.GetFoodInInventory();
            int requiredFoodInventory = state.Current.IsCity && state.OwnedCampCount > 0
                ? RecoveryServices.RequiredReturnFoodInventory(state)
                : spendReserves
                    ? state.Player.Group.Sum(character => Math.Max(0, 3 - character.Food))
                    : Math.Max(0, TradeTask.DesiredPortableFood(state) -
                        state.Player.Group.GetFoodReserve());
            int remainingWaterInventory = state.Player.Group.GetWaterInInventory();
            int requiredWaterInventory = state.Current.IsCity && state.OwnedCampCount > 0
                ? RecoveryServices.RequiredReturnWaterInventory(state)
                : 0;
            int acquiredWaterCapacity = AiItemPool.WaterContainerCapacity(target.Type);
            int remainingWaterCapacity = TradeTask.PortableWaterSupply(state) + acquiredWaterCapacity +
                temporaryPoolAssets.Sum(asset => AiItemPool.WaterContainerCapacity(asset.Type));
            int remainingMeleeWeapons = state.Player.Group.SelectMany(character => character.Items)
                .Count(item => item.DamageValue > 0 && !AiItemPool.IsFirearm(item.Type));
            Dictionary<string, int> remainingMaterials = TradeTask.ConstructionMaterials
                .ToDictionary(itemId => itemId, itemId => PortableMaterialCount(state, itemId));
            bool strategicPurchase = IsStrategicPurchase(state, target);
            foreach (TradeAsset candidate in allCandidates.Where(asset => asset.ID != target.ID))
            {
                if (!strategicPurchase && candidate.TradeValue >= target.TradeValue)
                    continue;
                if (!candidate.FromPool && candidate.FoodValue > 0 &&
                    remainingFoodInventory - candidate.FoodValue + target.FoodValue <
                        requiredFoodInventory)
                    continue;
                if (!candidate.FromPool && candidate.WaterValue > 0 &&
                    remainingWaterInventory - candidate.WaterValue + target.WaterValue <
                        requiredWaterInventory)
                    continue;
                // A completed production upgrade outranks standing container
                // reserves. Ordinary barter must preserve the full water target;
                // exceptional trap purchases may spend it but keep survival water.
                int waterFloor = spendReserves
                    ? state.Player.Group.Count * 3
                    : TradeTask.DesiredPortableWaterCapacity(state);
                if (AiItemPool.IsWaterContainer(candidate.Type) &&
                    remainingWaterCapacity - AiItemPool.WaterContainerCapacity(candidate.Type) < waterFloor)
                    continue;
                if (spendReserves && candidate.DamageValue > 0 &&
                    !AiItemPool.IsFirearm(candidate.Type) && remainingMeleeWeapons <= 1)
                    continue;
                if (!spendReserves && TradeTask.ConstructionMaterials.Contains(candidate.ID) &&
                    remainingMaterials[candidate.ID] <= DesiredMaterialStock(state, candidate.ID))
                    continue;

                offers.Add(candidate);
                offeredValue += candidate.TradeValue * TradeBenefit(state);
                if (!candidate.FromPool)
                {
                    remainingFoodInventory -= candidate.FoodValue;
                    remainingWaterInventory -= candidate.WaterValue;
                }
                remainingWaterCapacity -= AiItemPool.WaterContainerCapacity(candidate.Type);
                if (candidate.DamageValue > 0 && !AiItemPool.IsFirearm(candidate.Type))
                    remainingMeleeWeapons--;
                if (TradeTask.ConstructionMaterials.Contains(candidate.ID))
                    remainingMaterials[candidate.ID]--;
                if ((int)offeredValue >= (int)target.TradeValue)
                    break;
            }

            // Greedy accumulation can overshoot badly when the last item is
            // valuable. Remove dispensable offers from highest value to lowest as
            // long as the remaining bundle still meets the effective barter price.
            // Preserve empty containers before full ones: a full container is the
            // more useful trade asset and its cheaper empty form can be reacquired.
            foreach (TradeAsset candidate in offers
                .OrderBy(OfferRemovalGroup)
                .ThenByDescending(asset => asset.TradeValue)
                .ToArray())
            {
                if (offers.Count <= 1)
                    break;
                float withoutCandidate = offers
                    .Where(offer => offer != candidate)
                    .Sum(offer => offer.TradeValue * TradeBenefit(state));
                if ((int)withoutCandidate < (int)target.TradeValue)
                    continue;
                offers.Remove(candidate);
                offeredValue = withoutCandidate;
            }

            float barterBudget = offers.Sum(offer => offer.TradeValue * TradeBenefit(state));
            List<Item> targets = BuildReceivedBasket(state, trader, target, excludedTargets,
                barterBudget);
            int freedPortableSlots = offers.Count(offer => !offer.FromPool);
            int neededPortableSlots = targets.Count(item => !AiItemPool.Accepts(item.Type));
            bool canStoreTarget = neededPortableSlots <=
                state.Player.Group.GetFreeSlotCount() + freedPortableSlots;
            bool compressesCargo = strategicPurchase || offers.Count >= 2;
            float receivedUtility = targets.Sum(item => AcquisitionUtilityValue(state, item));
            bool avoidsSevereWaste = receivedUtility >= offers.Sum(offer => offer.TradeValue) * 0.65f;
            if (offers.Count > 0 && compressesCargo &&
                (int)offeredValue >= (int)target.TradeValue && canStoreTarget && avoidsSevereWaste)
                return new TradePlan(targets, offers);
        }

        return null;
    }

    internal static float TradeBenefit(ClassicAiState state) => state.RootGame.World.Difficulty switch
    {
        0 => 1.0f,
        1 => 1.2f,
        _ => 1.5f
    };

    internal static bool IsHighReturnLiquidReserve(Item item) =>
        item.FoodValue > 0 || AiItemPool.IsWaterContainer(item.Type) ||
        (item.DamageValue > 0 && !AiItemPool.IsFirearm(item.Type));

    static bool IsFullWaterContainer(Item item) =>
        item.WaterValue > 0 && item.Type.Empty != null;

    static bool IsFullWaterContainer(TradeAsset asset) =>
        asset.WaterValue > 0 && asset.Type.Empty != null;

    static bool IsEmptyWaterContainer(Item item) =>
        item.WaterValue == 0 && item.Type.Full?.WaterValue > 0;

    static int OfferRemovalGroup(Item item) =>
        IsEmptyWaterContainer(item) ? 0 : IsFullWaterContainer(item) ? 2 : 1;

    static int OfferRemovalGroup(TradeAsset asset) =>
        asset.WaterValue == 0 && asset.Type.Full?.WaterValue > 0
            ? 0
            : IsFullWaterContainer(asset) ? 2 : 1;

    static List<Item> BuildReceivedBasket(
        ClassicAiState state,
        Trader trader,
        Item primary,
        ISet<Item> excludedTargets,
        float barterBudget)
    {
        List<Item> targets = new() { primary };
        Item[] rankedFillers = trader.Items
            .Where(item => item != primary && item.TradeValue > 0 &&
                (excludedTargets == null || !excludedTargets.Contains(item)))
            .Select(item => new
            {
                Item = item,
                Priority = ShoppingPriority(state, item),
                CompletesRecipe = CompletesUsefulRecipe(state, item.ID)
            })
            .OrderByDescending(candidate => candidate.Priority > 0)
            .ThenByDescending(candidate => candidate.CompletesRecipe)
            .ThenByDescending(candidate => candidate.Item.TradeValue)
            .Select(candidate => candidate.Item)
            .ToArray();
        float remaining = barterBudget - primary.TradeValue;
        while (remaining >= 1)
        {
            Item filler = rankedFillers
                .Where(item => !targets.Contains(item) && item.TradeValue <= remaining)
                .FirstOrDefault();
            if (filler == null)
                break;
            targets.Add(filler);
            remaining -= filler.TradeValue;
        }
        return targets;
    }

    static float AcquisitionUtilityValue(ClassicAiState state, Item item) =>
        item.TradeValue * (CompletesUsefulRecipe(state, item.ID) ? 2f : 1f);

    internal static bool AdvancesOwnedMeatTrapRecipe(ClassicAiState state, string itemId)
    {
        string[] recipe = { "item_spring", "item_tin", "item_wire" };
        return recipe.Contains(itemId) && HasOwnedProductionNeed(state, "item_meat") &&
            recipe.Where(component => component != itemId)
                .Any(component => HasConstructionComponent(state, component));
    }

    internal static bool IsEarlyRareProductionPurchase(ClassicAiState state, string itemId)
    {
        if (state.OwnedCampCount >= 3 || !CanPrepareProductionInAdvance(state))
            return false;
        return itemId switch
        {
            "item_spring" or "item_tin" => HasPotentialProductionNeed(state, "item_meat"),
            "item_snake_trap" => HasPotentialProductionNeed(state, "item_snake"),
            _ => false
        };
    }

    internal static bool HasRegionalSnakeTrapNeed(ClassicAiState state) => state.RootGame.World.Locations
        .Where(camp => camp.Player == state.Player && LocalOpportunities.ShouldPreferProductionAtCamp(state, camp))
        .Where(camp => NeedsProductionResult(state, camp, "item_snake"))
        .Select(camp => RouteFinder.Find(state.Player, state.Current, camp))
        .Any(route => route != null && route.Days <= 7);

    internal static bool HasStrategicSnakeTrapNeed(ClassicAiState state) =>
        HasPotentialProductionNeed(state, "item_snake");

    internal static List<TradeAsset> SnapshotSurplusPoolAssets(ClassicAiState state)
    {
        List<TradeAsset> assets = new();
        List<ItemType> remaining = state.Pool.SnapshotItemTypes().ToList();
        int requiredPoolCapacity = System.Math.Max(0,
            RequiredUnstationedWaterContainerCapacity(state) - PortableWaterCapacity(state));
        int remainingPoolCapacity = state.Pool.TotalWaterContainerCapacity;
        foreach (ItemType type in remaining
            .Where(AiItemPool.IsWaterContainer)
            .OrderBy(AiItemPool.WaterContainerCapacity)
            .ThenBy(type => type.WaterValue)
            .ToArray())
        {
            int capacity = AiItemPool.WaterContainerCapacity(type);
            if (remainingPoolCapacity - capacity < requiredPoolCapacity)
                break;
            assets.Add(new TradeAsset(null, null, type, true));
            remaining.Remove(type);
            remainingPoolCapacity -= capacity;
        }
        int surplusProtection = System.Math.Max(0,
            GlobalProtectionStock(state) - DesiredProtectionReserve(state));
        foreach (ItemType type in remaining
            .Where(AiItemPool.IsHazardProtection)
            .OrderBy(type => type.ID == "item_paper_helmet" ? 0 :
                type.ID == "item_gas_mask" ? 1 : 2)
            .Take(surplusProtection)
            .ToArray())
        {
            assets.Add(new TradeAsset(null, null, type, true));
            remaining.Remove(type);
        }
        // Three portable production tools are enough to seed the next camps. Convert
        // additional low-tier tools into denser trade value instead of hoarding them.
        foreach (ItemType type in remaining
            .Where(type => type.Production != null)
            .OrderBy(type => type.Production.Produce.TradeValue)
            .ThenBy(type => type.DamageValue)
            .Take(System.Math.Max(0, state.Pool.ProductionToolCount - 3))
            .ToArray())
        {
            assets.Add(new TradeAsset(null, null, type, true));
            remaining.Remove(type);
        }
        foreach (ItemType type in remaining
            .Where(AiItemPool.IsFirearm)
            .OrderByDescending(type => type.TradeValue)
            .ToArray())
        {
            assets.Add(new TradeAsset(null, null, type, true));
            remaining.Remove(type);
        }
        return assets;
    }

    internal sealed record TradeAsset(
        IItemCollection Owner,
        Item? Item,
        ItemType Type,
        bool FromPool)
    {
        public TradeAsset(IItemCollection owner, Item item, bool fromPool)
            : this(owner, item, item.Type, fromPool)
        {
        }

        public string ID => Type.ID;
        public float TradeValue => Type.TradeValue;
        public int FoodValue => Type.FoodValue;
        public int WaterValue => Type.WaterValue;
        public int DamageValue => Type.DamageValue;
    }
    internal sealed record TradePlan(
        List<Item> Targets,
        List<TradeAsset> Offers);
    internal sealed class TradeFailureState
    {
        public string Signature;
    }
}
