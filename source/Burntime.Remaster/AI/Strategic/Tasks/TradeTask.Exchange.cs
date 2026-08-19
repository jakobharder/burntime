using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class TradeTask
{
    internal static void TradeWithTrader(ClassicAiState state, Trader trader)
    {
        if (trader == null || trader.Items.Count == 0)
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
            trader.Items.Remove(plan.Target);
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

            if (AiItemPool.Accepts(plan.Target.Type))
                state.Pool.Insert(plan.Target);
            else
                state.Player.Group.First(character => !character.Items.IsFull).Items.Add(plan.Target);

            RestoreUnusedPoolAssets(state, plan.TemporaryPoolAssets.Except(plan.Offers));
            completed++;
            string action = IsStrategicPurchase(state, plan.Target) ? "traded" : "consolidated";
            AiTelemetry.Report(state.Player,
                $"{action} {string.Join(", ", plan.Offers.Select(offer => offer.Item.ID))} for " +
                $"{plan.Target.ID} with {trader.Name} (value {offeredValue:0} -> {plan.Target.TradeValue:0}, " +
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
                    (HasRegionalSnakeTrapNeed(state) || HasOwnedProductionNeed(state, "item_snake")) ||
                AdvancesOwnedMeatTrapRecipe(state, target.ID) ||
                IsEarlyRareProductionPurchase(state, target.ID);
            List<TradeAsset> allCandidates = state.Player.Group
                .SelectMany(character => character.Items
                    .Select(item => new TradeAsset(character.Items, item, false)))
                .Where(asset => CanSell(state, asset.Item) ||
                    (spendReserves && IsHighReturnLiquidReserve(asset.Item)))
                .Concat(temporaryPoolAssets)
                .OrderBy(asset => asset.Item.TradeValue)
                .ThenBy(asset => SalePriority(asset.Item))
                .ToList();
            List<TradeAsset> offers = new();
            float offeredValue = 0;
            int remainingFood = TradeTask.PortableFoodSupply(state);
            int remainingWaterCapacity = TradeTask.PortableWaterSupply(state) + temporaryPoolAssets
                .Sum(asset => AiItemPool.WaterContainerCapacity(asset.Item.Type));
            int remainingMeleeWeapons = state.Player.Group.SelectMany(character => character.Items)
                .Count(item => item.DamageValue > 0 && !AiItemPool.IsFirearm(item.Type));
            Dictionary<string, int> remainingMaterials = TradeTask.ConstructionMaterials
                .ToDictionary(itemId => itemId, itemId => PortableMaterialCount(state, itemId));
            bool strategicPurchase = IsStrategicPurchase(state, target);
            foreach (TradeAsset candidate in allCandidates.Where(asset => asset.Item.ID != target.ID))
            {
                if (!strategicPurchase && candidate.Item.TradeValue >= target.TradeValue)
                    continue;
                if (!spendReserves && candidate.Item.FoodValue > 0 && remainingFood - candidate.Item.FoodValue +
                    target.FoodValue < TradeTask.DesiredPortableFood(state))
                    continue;
                if (!spendReserves && AiItemPool.IsWaterContainer(candidate.Item.Type) &&
                    remainingWaterCapacity - AiItemPool.WaterContainerCapacity(candidate.Item.Type) +
                        AiItemPool.WaterContainerCapacity(target.Type) < TradeTask.DesiredPortableWaterCapacity(state))
                    continue;
                if (spendReserves && candidate.Item.DamageValue > 0 &&
                    !AiItemPool.IsFirearm(candidate.Item.Type) && remainingMeleeWeapons <= 1)
                    continue;
                if (TradeTask.ConstructionMaterials.Contains(candidate.Item.ID) &&
                    remainingMaterials[candidate.Item.ID] <= DesiredMaterialStock(state, candidate.Item.ID))
                    continue;

                offers.Add(candidate);
                offeredValue += candidate.Item.TradeValue * TradeBenefit(state);
                remainingFood -= candidate.Item.FoodValue;
                remainingWaterCapacity -= AiItemPool.WaterContainerCapacity(candidate.Item.Type);
                if (candidate.Item.DamageValue > 0 && !AiItemPool.IsFirearm(candidate.Item.Type))
                    remainingMeleeWeapons--;
                if (TradeTask.ConstructionMaterials.Contains(candidate.Item.ID))
                    remainingMaterials[candidate.Item.ID]--;
                if ((int)offeredValue >= (int)target.TradeValue)
                    break;
            }

            bool canStoreTarget = AiItemPool.Accepts(target.Type) ||
                state.Player.Group.GetFreeSlotCount() > 0 || offers.Any(offer => !offer.FromPool);
            bool compressesCargo = strategicPurchase || offers.Count >= 2;
            if (offers.Count > 0 && compressesCargo &&
                (int)offeredValue >= (int)target.TradeValue && canStoreTarget)
                return new TradePlan(target, offers, temporaryPoolAssets);
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

    internal static List<TradeAsset> TakeSurplusPoolAssets(ClassicAiState state)
    {
        List<TradeAsset> assets = new();
        while (state.Pool.WaterContainerCount > 1)
        {
            Item item = state.Pool.TakeLeastWaterContainer();
            if (item == null)
                break;
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
            state.Pool.Insert(asset.Item);
    }

    internal sealed record TradeAsset(IItemCollection Owner, Item Item, bool FromPool);
    internal sealed record TradePlan(
        Item Target,
        List<TradeAsset> Offers,
        List<TradeAsset> TemporaryPoolAssets);
    internal sealed class TradeFailureState
    {
        public string Signature;
    }
}
