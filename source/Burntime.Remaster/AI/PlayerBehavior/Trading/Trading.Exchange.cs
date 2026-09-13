using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.AI;

internal static partial class Trading
{
    internal static bool TradeWithTrader(ClassicAiState state, Trader trader)
    {
        if (trader == null)
            return false;

        StrategicNeeds needs = AiTurnContext.For(state).Needs;
        bool firstExposureToday = EconomicSupport.RecordTraderExposure(state, trader);
        Item? snakeTrap = trader.Items.FirstOrDefault(item => item.ID == "item_snake_trap");
        bool snakeTrapAvailable = snakeTrap != null;
        bool demandedSnakeTrapAvailable = snakeTrap != null && needs.NeedsProduction(snakeTrap.Type);
        if (demandedSnakeTrapAvailable)
            EconomicSupport.StartSnakeTrapCampaign(state);
        if (firstExposureToday && snakeTrapAvailable)
            AiTelemetry.Report(state.Player,
                $"encountered item_snake_trap with {trader.Name}" +
                (demandedSnakeTrapAvailable ? " for current or future camp production" : string.Empty));
        if (trader.Items.Count == 0)
            return false;

        HashSet<Item> soldToTrader = new();
        int completed = 0;
        bool madeStrategicPurchase = false;
        TradePlan Plan(bool allowStrategicPurchase)
            => CreateTradePlan(state, trader, soldToTrader, allowStrategicPurchase);
        TradePlan nextPlan = Plan(allowStrategicPurchase: true);
        if (nextPlan != null)
        {
            int capacity = state.Player.Party.Sum(character => character.Items.MaxCount);
            int cargo = capacity - state.Player.Party.GetFreeSlotCount();
            float sellableValue = state.Player.Party.SelectMany(character => character.Items)
                .Where(item => CanSell(state, item))
                .Sum(item => item.TradeValue);
            string visit = state.Current.IsCity
                ? Trading.HasPreparedTradeCargo(state) ? "prepared city" : "incidental city"
                : "roaming";
            AiTelemetry.Report(state.Player,
                $"{visit} barter with {trader.Name}: cargo {cargo}/{capacity} slots, " +
                $"sellable value {sellableValue:0}");
        }

        for (int exchange = 0; exchange < 2; exchange++)
        {
            TradePlan plan = nextPlan;
            if (plan == null || !CanAfford(state, plan.Offers, plan.Targets))
                break;

            for (int index = 0; index < plan.Offers.Count; index++)
            {
                TradeAsset offer = plan.Offers[index];
                if (!offer.FromPool)
                    continue;
                Item item = state.Reserve.TakeForTrade(offer.Type) ??
                    throw new InvalidOperationException("planned pool trade asset is no longer available");
                plan.Offers[index] = new TradeAsset(null, item, true);
            }
            float offeredValue = plan.Offers.Sum(offer => offer.TradeValue);
            foreach (Item target in plan.Targets)
                trader.Items.Remove(target);
            foreach (TradeAsset offer in plan.Offers.Where(offer => !offer.FromPool))
            {
                Character owner = state.Player.Party
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
                if (!AiItemPool.Accepts(target.Type) || !state.Reserve.Insert(target))
                    (GroupInventory.FindCargoCarrier(state, target) ??
                        throw new InvalidOperationException("planned trade target no longer fits reserved cargo roles"))
                        .Items.Add(target);
            }

            EquipmentPlanning.Maintain(state);
            completed++;
            if (plan.Targets.Any(target => target.ID == "item_snake_trap"))
                EconomicSupport.CompleteSnakeTrapCampaign(state);
            bool strategicPurchase = plan.Targets.Any(target => needs.IsStrategic(target.Type));
            madeStrategicPurchase |= strategicPurchase;
            string action = strategicPurchase
                ? "traded"
                : "consolidated";
            float receivedValue = plan.Targets.Sum(target => target.TradeValue);
            AiTelemetry.Report(state.Player,
                $"{action} {string.Join(", ", plan.Offers.Select(offer => offer.ID))} for " +
                $"{string.Join(", ", plan.Targets.Select(target => target.ID))} with {trader.Name} " +
                $"(value {offeredValue:0} -> {receivedValue:0}, " +
                $"barter value x{plan.AppliedTradeFactor:0.00})");

            AiTurnContext.For(state).RefreshNeeds();
            needs = AiTurnContext.For(state).Needs;
            if (exchange + 1 < 2)
                nextPlan = Plan(allowStrategicPurchase: !madeStrategicPurchase);
        }

        if (completed > 0)
        {
            Trading.LastReportedTradeFailure.Remove(state.Player);
            return true;
        }

        if (AiTelemetry.Sink != null &&
            trader.Items.Any(item => ShoppingPriority(state, item) > 0) &&
            state.Player.Party.SelectMany(character => character.Items).Any(item => CanSell(state, item)))
        {
            string signature = trader.Name;
            TradeFailureState failure = Trading.LastReportedTradeFailure.GetOrCreateValue(state.Player);
            if (failure.Signature != signature)
            {
                AiTelemetry.Report(state.Player,
                    $"could not complete a useful trade with {trader.Name}: insufficient safe offers or inventory space");
                failure.Signature = signature;
            }
        }
        return false;
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

    internal static TradePlan CreateTradePlan(
        ClassicAiState state,
        Trader trader,
        ISet<Item> excludedTargets = null,
        bool allowStrategicPurchase = true)
    {
        if (trader == null || trader.Items.Count == 0)
            return null;

        StrategicNeeds needs = AiTurnContext.For(state).Needs;
        List<TradeAsset> temporaryPoolAssets = SnapshotSurplusPoolAssets(state, needs);
        foreach (Item target in trader.Items
            .Where(item => item.TradeValue > 0 &&
                (excludedTargets == null || !excludedTargets.Contains(item)))
            .Select(item => new { Item = item, Priority = ShoppingPriority(state, item) })
            .Where(candidate => candidate.Priority > 0 &&
                (allowStrategicPurchase || !needs.IsStrategic(candidate.Item.Type)))
            .OrderByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Item.TradeValue)
            .Select(candidate => candidate.Item)
            .Take(3))
        {
            List<TradeAsset> allCandidates = state.Player.Party
                .SelectMany(character => character.Items
                    .Select(item => new TradeAsset(character.Items, item, false)))
                .Where(asset => CanSell(state, asset.Item!))
                .Concat(temporaryPoolAssets)
                .OrderBy(asset => IsFullWaterContainer(asset) ? 0 : 1)
                .ThenBy(asset => asset.TradeValue)
                .ThenBy(asset => asset.Item == null ? 3 : SalePriority(asset.Item))
                .ToList();
            List<TradeAsset> offers = new();
            int remainingFoodInventory = state.Player.Party.GetFoodInInventory();
            int requiredFoodInventory = state.Current.IsCity && state.OwnedCampCount > 0
                ? RecoveryServices.RequiredReturnFoodInventory(state)
                : Math.Max(0, needs.DesiredPortableFood - state.Player.Party.GetFoodReserve());
            int remainingWaterInventory = state.Player.Party.GetWaterInInventory();
            int requiredWaterInventory = state.Current.IsCity && state.OwnedCampCount > 0
                ? RecoveryServices.RequiredReturnWaterInventory(state)
                : 0;
            int acquiredWaterCapacity = AiItemPool.WaterContainerCapacity(target.Type);
            int remainingWaterCapacity = Trading.PortableWaterSupply(state) + acquiredWaterCapacity +
                temporaryPoolAssets.Sum(asset => AiItemPool.WaterContainerCapacity(asset.Type));
            int remainingMeleeWeapons = state.Player.Party.SelectMany(character => character.Items)
                .Count(item => item.DamageValue > 0 && !AiItemPool.IsFirearm(item.Type));
            Dictionary<string, int> remainingMaterials = Trading.ConstructionMaterials
                .ToDictionary(itemId => itemId, itemId => PortableMaterialCount(state, itemId));
            bool strategicPurchase = needs.IsStrategic(target.Type);
            float appliedTradeFactor = TradeFactor(state);
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
                int waterFloor = needs.DesiredPortableWaterCapacity;
                if (AiItemPool.IsWaterContainer(candidate.Type) &&
                    remainingWaterCapacity - AiItemPool.WaterContainerCapacity(candidate.Type) < waterFloor)
                    continue;
                if (candidate.DamageValue > 0 &&
                    !AiItemPool.IsFirearm(candidate.Type) &&
                    remainingMeleeWeapons <= needs.MeleeWeaponQuota)
                    continue;
                if (Trading.ConstructionMaterials.Contains(candidate.ID) &&
                    remainingMaterials[candidate.ID] <= needs.MaterialQuota(candidate.ID))
                    continue;

                offers.Add(candidate);
                if (!candidate.FromPool)
                {
                    remainingFoodInventory -= candidate.FoodValue;
                    remainingWaterInventory -= candidate.WaterValue;
                }
                remainingWaterCapacity -= AiItemPool.WaterContainerCapacity(candidate.Type);
                if (candidate.DamageValue > 0 && !AiItemPool.IsFirearm(candidate.Type))
                    remainingMeleeWeapons--;
                if (Trading.ConstructionMaterials.Contains(candidate.ID))
                    remainingMaterials[candidate.ID]--;
                if (CanAfford(state, offers, new[] { target }))
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
                if (!CanAfford(state, offers.Where(offer => offer != candidate), new[] { target }))
                    continue;
                offers.Remove(candidate);
            }

            float barterBudget = TradeValue(state, offers.Select(offer => offer.TradeValue));
            List<Item> targets = BuildReceivedBasket(state, needs, trader, target, excludedTargets,
                barterBudget, allowStrategicPurchase);
            int freedPortableSlots = offers.Count(offer => !offer.FromPool);
            int neededPortableSlots = targets.Count(item => !AiItemPool.Accepts(item.Type));
            Item[] removedLeaderItems = offers
                .Where(offer => !offer.FromPool &&
                    offer.Owner == state.Player.Character.Items)
                .Select(offer => offer.Item!)
                .ToArray();
            Item[] addedPortableItems = targets
                .Where(item => !AiItemPool.Accepts(item.Type))
                .ToArray();
            int reservedLeaderSlots = GroupInventory.MissingLeaderRoleSlotsAfter(
                state, removedLeaderItems, addedPortableItems);
            bool canStoreTarget = neededPortableSlots <=
                state.Player.Party.GetFreeSlotCount() + freedPortableSlots - reservedLeaderSlots;
            bool compressesCargo = strategicPurchase || offers.Count >= 2;
            float receivedUtility = targets.Sum(item => AcquisitionUtilityValue(state, item));
            bool avoidsSevereWaste = receivedUtility >= barterBudget * 0.65f;
            if (offers.Count > 0 && compressesCargo &&
                CanAfford(state, offers, targets) && canStoreTarget && avoidsSevereWaste)
                return new TradePlan(targets, offers, appliedTradeFactor);
        }

        return null;
    }

    internal static float TradeFactor(ClassicAiState state) =>
        RuleFormulas.EffectiveTradeFactor(
            BarterFactor(state), AiPolicy.ForDifficulty(state.Difficulty).TradeBenefit);

    internal static float TradeValue(ClassicAiState state, IEnumerable<float> values) =>
        RuleFormulas.EffectiveTradeValue(
            values, BarterFactor(state), AiPolicy.ForDifficulty(state.Difficulty).TradeBenefit);

    static int BarterFactor(ClassicAiState state) =>
        state.RootGame.RuleBook.Settings.GetBarterFactor(state.RootGame.World.Difficulty);

    static bool CanAfford(ClassicAiState state, IEnumerable<TradeAsset> offers, IEnumerable<Item> targets) =>
        RuleFormulas.AcceptTrade(offers.Select(offer => offer.TradeValue),
            targets.Select(item => item.TradeValue), BarterFactor(state),
            AiPolicy.ForDifficulty(state.Difficulty).TradeBenefit);

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
        StrategicNeeds needs,
        Trader trader,
        Item primary,
        ISet<Item> excludedTargets,
        float barterBudget,
        bool allowStrategicPurchase)
    {
        List<Item> targets = new() { primary };
        Item[] rankedFillers = trader.Items
            .Where(item => item != primary && item.TradeValue > 0 &&
                (excludedTargets == null || !excludedTargets.Contains(item)))
            .Select(item => new
            {
                Item = item,
                Priority = ShoppingPriority(state, item),
                CompletesRecipe = needs.CompletesUsefulRecipe(item.ID)
            })
            .Where(candidate => allowStrategicPurchase ||
                !needs.IsStrategic(candidate.Item.Type))
            .OrderByDescending(candidate => candidate.Priority > 0)
            .ThenByDescending(candidate => candidate.CompletesRecipe)
            .ThenByDescending(candidate => candidate.Item.TradeValue)
            .Select(candidate => candidate.Item)
            .ToArray();
        float remaining = barterBudget - primary.TradeValue;
        while (remaining >= 1)
        {
            Item filler = rankedFillers
                .Where(item => !targets.Contains(item) && item.TradeValue <= remaining &&
                    needs.CanBuy(item.Type, targets.Count(target => target.Type == item.Type)) &&
                    (!AiItemPool.IsFirearm(item.Type) ||
                        targets.Count(target => AiItemPool.IsFirearm(target.Type)) < needs.EquipmentDemand(item.Type)))
                .FirstOrDefault();
            if (filler == null)
                break;
            targets.Add(filler);
            remaining -= filler.TradeValue;
        }
        return targets;
    }

    static float AcquisitionUtilityValue(ClassicAiState state, Item item) =>
        item.TradeValue * (AiTurnContext.For(state).Needs.CompletesUsefulRecipe(item.ID) ? 2f : 1f);

    internal static bool HasStrategicSnakeTrapNeed(ClassicAiState state) =>
        AiTurnContext.For(state).Needs.NeedsProduction(
            state.RootGame.ItemTypes["item_snake_trap"]);

    internal static List<TradeAsset> SnapshotSurplusPoolAssets(
        ClassicAiState state,
        StrategicNeeds? needs = null)
    {
        needs ??= AiTurnContext.For(state).Needs;
        List<TradeAsset> assets = new();
        List<ItemType> remaining = state.Reserve.SnapshotItemTypes().ToList();
        int requiredPoolCapacity = System.Math.Max(0,
            needs.DesiredPortableWaterCapacity - PortableWaterCapacity(state));
        int remainingPoolCapacity = state.Reserve.TotalWaterContainerCapacity;
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
            needs.ProtectionStock - needs.ProtectionQuota);
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
        // Clothing above this difficulty's cap is useful barter cargo, not a
        // permanent reserve that Easy/Normal can never equip.
        foreach (ItemType type in remaining.Where(type =>
            type.DefenseValue > AiPolicy.ForDifficulty(state.Difficulty).ArmourLimit &&
            !AiItemPool.IsHazardProtection(type)).ToArray())
        {
            assets.Add(new TradeAsset(null, null, type, true));
            remaining.Remove(type);
        }
        int surplusProductionTools = System.Math.Max(0,
            needs.ProductionToolStock - needs.ProductionToolQuota);
        foreach (ItemType type in remaining
            .Where(type => type.Production != null && !needs.NeedsProduction(type))
            .OrderBy(type => type.Production.Produce.TradeValue)
            .ThenBy(type => type.DamageValue)
            .Take(surplusProductionTools)
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
        List<TradeAsset> Offers,
        float AppliedTradeFactor);
    internal sealed class TradeFailureState
    {
        public string Signature;
    }
}
