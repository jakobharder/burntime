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

            float offeredValue = plan.Offers.Sum(offer => offer.Item.TradeValue);
            foreach (Item target in plan.Targets)
                trader.Items.Remove(target);
            foreach (TradeAsset offer in plan.Offers)
            {
                if (!offer.FromPool)
                {
                    Character owner = state.Player.Group
                        .FirstOrDefault(character => character.Items == offer.Owner);
                    if (owner?.Weapon == offer.Item)
                        owner.Weapon = null;
                    offer.Owner.Remove(offer.Item);
                }
                soldToTrader.Add(offer.Item);
            }

            PruneAndStoreTraderOffers(trader, plan.Offers.Select(offer => offer.Item));

            foreach (Item target in plan.Targets)
            {
                if (AiItemPool.Accepts(target.Type))
                    state.Pool.Insert(target);
                else
                    state.Player.Group.First(character => !character.Items.IsFull).Items.Add(target);
            }

            RestoreUnusedPoolAssets(state, plan.TemporaryPoolAssets.Except(plan.Offers));
            completed++;
            if (plan.Targets.Any(target => target.ID == "item_snake_trap"))
                EconomicSupport.CompleteSnakeTrapCampaign(state);
            string action = plan.Targets.Any(target => IsStrategicPurchase(state, target))
                ? "traded"
                : "consolidated";
            float receivedValue = plan.Targets.Sum(target => target.TradeValue);
            AiTelemetry.Report(state.Player,
                $"{action} {string.Join(", ", plan.Offers.Select(offer => offer.Item.ID))} for " +
                $"{string.Join(", ", plan.Targets.Select(target => target.ID))} with {trader.Name} " +
                $"(value {offeredValue:0} -> {receivedValue:0}, " +
                $"AI barter value x{TradeBenefit(state):0.0})");

            // Firearm parts are never shopping goals, but when normal value trading
            // happens to put both pieces together, convert them into the denser item.
            LocalOpportunities.ConstructPortableWeapon(state);
            nextPlan = CreateTradePlan(state, trader, soldToTrader);
        }

        // A seventh hypothetical exchange may have pulled temporary pool goods
        // while planning. It was not executed, so return every asset it inspected.
        if (nextPlan != null)
            RestoreUnusedPoolAssets(state, nextPlan.TemporaryPoolAssets);

        if (completed > 0)
        {
            TradeTask.LastReportedTradeFailure.Remove(state.Player);
            return;
        }

        if (trader.Items.Any(item => ShoppingPriority(state, item) > 0) &&
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
        if (plan == null)
            return false;
        RestoreUnusedPoolAssets(state, plan.TemporaryPoolAssets);
        return true;
    }

    internal static TradePlan CreateTradePlan(ClassicAiState state, Trader trader, ISet<Item> excludedTargets = null)
    {
        if (trader == null || trader.Items.Count == 0)
            return null;

        List<TradeAsset> temporaryPoolAssets = TakeSurplusPoolAssets(state);

        foreach (Item target in trader.Items
            .Where(item => item.TradeValue > 0 && ShoppingPriority(state, item) > 0 &&
                (excludedTargets == null || !excludedTargets.Contains(item)))
            .OrderByDescending(item => ShoppingPriority(state, item))
            .ThenBy(item => item.TradeValue))
        {
            bool spendReserves =
                target.ID == "item_snake_trap" &&
                    HasStrategicSnakeTrapNeed(state) ||
                AdvancesOwnedMeatTrapRecipe(state, target.ID) ||
                IsEarlyRareProductionPurchase(state, target.ID);
            List<TradeAsset> exceptionalPoolAssets = target.ID == "item_snake_trap" && spendReserves
                ? state.Pool.TakeConstructionMaterials()
                    .Select(item => new TradeAsset(null, item, true))
                    .ToList()
                : new List<TradeAsset>();
            List<TradeAsset> allCandidates = state.Player.Group
                .SelectMany(character => character.Items
                    .Select(item => new TradeAsset(character.Items, item, false)))
                .Where(asset => CanSell(state, asset.Item) ||
                    (spendReserves && (IsHighReturnLiquidReserve(asset.Item) ||
                        TradeTask.ConstructionMaterials.Contains(asset.Item.ID)) &&
                        !(AiItemPool.IsWaterContainer(asset.Item.Type) &&
                            NeedsCriticalWaterWaypointUpgrade(state))))
                .Concat(temporaryPoolAssets)
                .Concat(exceptionalPoolAssets)
                .OrderBy(asset => IsFullWaterContainer(asset.Item) ? 0 : 1)
                .ThenBy(asset => asset.Item.TradeValue)
                .ThenBy(asset => SalePriority(asset.Item))
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
                temporaryPoolAssets.Sum(asset => AiItemPool.WaterContainerCapacity(asset.Item.Type));
            int remainingMeleeWeapons = state.Player.Group.SelectMany(character => character.Items)
                .Count(item => item.DamageValue > 0 && !AiItemPool.IsFirearm(item.Type));
            Dictionary<string, int> remainingMaterials = TradeTask.ConstructionMaterials
                .ToDictionary(itemId => itemId, itemId => PortableMaterialCount(state, itemId));
            bool strategicPurchase = IsStrategicPurchase(state, target);
            foreach (TradeAsset candidate in allCandidates.Where(asset => asset.Item.ID != target.ID))
            {
                if (!strategicPurchase && candidate.Item.TradeValue >= target.TradeValue)
                    continue;
                if (!candidate.FromPool && candidate.Item.FoodValue > 0 &&
                    remainingFoodInventory - candidate.Item.FoodValue + target.FoodValue <
                        requiredFoodInventory)
                    continue;
                if (!candidate.FromPool && candidate.Item.WaterValue > 0 &&
                    remainingWaterInventory - candidate.Item.WaterValue + target.WaterValue <
                        requiredWaterInventory)
                    continue;
                // A completed production upgrade outranks standing container
                // reserves. Ordinary barter must preserve the full water target;
                // exceptional trap purchases may spend it but keep survival water.
                int waterFloor = spendReserves
                    ? state.Player.Group.Count * 3
                    : TradeTask.DesiredPortableWaterCapacity(state);
                if (AiItemPool.IsWaterContainer(candidate.Item.Type) &&
                    remainingWaterCapacity - AiItemPool.WaterContainerCapacity(candidate.Item.Type) < waterFloor)
                    continue;
                if (spendReserves && candidate.Item.DamageValue > 0 &&
                    !AiItemPool.IsFirearm(candidate.Item.Type) && remainingMeleeWeapons <= 1)
                    continue;
                if (!spendReserves && TradeTask.ConstructionMaterials.Contains(candidate.Item.ID) &&
                    remainingMaterials[candidate.Item.ID] <= DesiredMaterialStock(state, candidate.Item.ID))
                    continue;

                offers.Add(candidate);
                offeredValue += candidate.Item.TradeValue * TradeBenefit(state);
                if (!candidate.FromPool)
                {
                    remainingFoodInventory -= candidate.Item.FoodValue;
                    remainingWaterInventory -= candidate.Item.WaterValue;
                }
                remainingWaterCapacity -= AiItemPool.WaterContainerCapacity(candidate.Item.Type);
                if (candidate.Item.DamageValue > 0 && !AiItemPool.IsFirearm(candidate.Item.Type))
                    remainingMeleeWeapons--;
                if (TradeTask.ConstructionMaterials.Contains(candidate.Item.ID))
                    remainingMaterials[candidate.Item.ID]--;
                if ((int)offeredValue >= (int)target.TradeValue)
                    break;
            }

            // Greedy accumulation can overshoot badly when the last item is
            // valuable. Remove dispensable offers from highest value to lowest as
            // long as the remaining bundle still meets the effective barter price.
            // Preserve empty containers before full ones: a full container is the
            // more useful trade asset and its cheaper empty form can be reacquired.
            foreach (TradeAsset candidate in offers
                .OrderBy(asset => OfferRemovalGroup(asset.Item))
                .ThenByDescending(asset => asset.Item.TradeValue)
                .ToArray())
            {
                if (offers.Count <= 1)
                    break;
                float withoutCandidate = offers
                    .Where(offer => offer != candidate)
                    .Sum(offer => offer.Item.TradeValue * TradeBenefit(state));
                if ((int)withoutCandidate < (int)target.TradeValue)
                    continue;
                offers.Remove(candidate);
                offeredValue = withoutCandidate;
            }

            float barterBudget = offers.Sum(offer => offer.Item.TradeValue * TradeBenefit(state));
            List<Item> targets = BuildReceivedBasket(state, trader, target, excludedTargets,
                barterBudget);
            int freedPortableSlots = offers.Count(offer => !offer.FromPool);
            int neededPortableSlots = targets.Count(item => !AiItemPool.Accepts(item.Type));
            bool canStoreTarget = neededPortableSlots <=
                state.Player.Group.GetFreeSlotCount() + freedPortableSlots;
            bool compressesCargo = strategicPurchase || offers.Count >= 2;
            float receivedUtility = targets.Sum(item => AcquisitionUtilityValue(state, item));
            bool avoidsSevereWaste = receivedUtility >= offers.Sum(offer => offer.Item.TradeValue) * 0.65f;
            if (offers.Count > 0 && compressesCargo &&
                (int)offeredValue >= (int)target.TradeValue && canStoreTarget && avoidsSevereWaste)
                return new TradePlan(targets, offers,
                    temporaryPoolAssets.Concat(exceptionalPoolAssets).ToList());

            RestoreUnusedPoolAssets(state, exceptionalPoolAssets);
        }

        RestoreUnusedPoolAssets(state, temporaryPoolAssets);
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

    static bool IsEmptyWaterContainer(Item item) =>
        item.WaterValue == 0 && item.Type.Full?.WaterValue > 0;

    static int OfferRemovalGroup(Item item) =>
        IsEmptyWaterContainer(item) ? 0 : IsFullWaterContainer(item) ? 2 : 1;

    static List<Item> BuildReceivedBasket(
        ClassicAiState state,
        Trader trader,
        Item primary,
        ISet<Item> excludedTargets,
        float barterBudget)
    {
        List<Item> targets = new() { primary };
        float remaining = barterBudget - primary.TradeValue;
        while (remaining >= 1)
        {
            Item filler = trader.Items
                .Where(item => item != primary && !targets.Contains(item) && item.TradeValue > 0 &&
                    item.TradeValue <= remaining &&
                    (excludedTargets == null || !excludedTargets.Contains(item)))
                .OrderByDescending(item => ShoppingPriority(state, item) > 0)
                .ThenByDescending(item => CompletesUsefulRecipe(state, item.ID))
                .ThenByDescending(item => item.TradeValue)
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

    internal static List<TradeAsset> TakeSurplusPoolAssets(ClassicAiState state)
    {
        List<TradeAsset> assets = new();
        int requiredPoolCapacity = System.Math.Max(0,
            RequiredUnstationedWaterContainerCapacity(state) - PortableWaterCapacity(state));
        while (state.Pool.WaterContainerCount > 0)
        {
            Item item = state.Pool.TakeLeastWaterContainer();
            if (item == null)
                break;
            if (state.Pool.TotalWaterContainerCapacity < requiredPoolCapacity)
            {
                state.Pool.Insert(item);
                break;
            }
            assets.Add(new TradeAsset(null, item, true));
        }
        while (GlobalProtectionStock(state) > DesiredProtectionReserve(state) && state.Pool.ProtectionCount > 0)
        {
            Item item = state.Pool.TakeLeastProtection();
            if (item == null)
                break;
            assets.Add(new TradeAsset(null, item, true));
        }
        // Three portable production tools are enough to seed the next camps. Convert
        // additional low-tier tools into denser trade value instead of hoarding them.
        while (state.Pool.ProductionToolCount > 3)
        {
            Item item = state.Pool.TakeLeastProductionTool();
            if (item == null)
                break;
            assets.Add(new TradeAsset(null, item, true));
        }
        while (state.Pool.FirearmCount > 0)
        {
            Item item = state.Pool.TakeBestTradeFirearm();
            if (item == null)
                break;
            assets.Add(new TradeAsset(null, item, true));
        }
        return assets;
    }

    internal static void RestoreUnusedPoolAssets(ClassicAiState state, IEnumerable<TradeAsset> assets)
    {
        foreach (TradeAsset asset in assets.Where(asset => asset.FromPool))
        {
            if (TradeTask.ConstructionMaterials.Contains(asset.Item.ID))
                state.Pool.TryReserveConstructionMaterial(asset.Item);
            else
                state.Pool.Insert(asset.Item);
        }
    }

    internal sealed record TradeAsset(IItemCollection Owner, Item Item, bool FromPool);
    internal sealed record TradePlan(
        List<Item> Targets,
        List<TradeAsset> Offers,
        List<TradeAsset> TemporaryPoolAssets);
    internal sealed class TradeFailureState
    {
        public string Signature;
    }
}
