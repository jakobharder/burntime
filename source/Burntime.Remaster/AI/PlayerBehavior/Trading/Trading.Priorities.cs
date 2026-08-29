using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class Trading
{
    internal static int DesiredCampWaterContainerCount(Location camp) =>
        1 + camp.CampNPC.Count(character => !character.IsDead);

    internal sealed class AssortmentShoppingPriorityState
    {
        public StrategicNeeds Needs { get; }

        public AssortmentShoppingPriorityState(ClassicAiState state) =>
            Needs = AiTurnContext.For(state).Needs;
    }

    internal static float ShoppingPriority(ClassicAiState state, Item item)
    {
        float strategic = ShoppingPriority(
            AiTurnContext.For(state).Needs, item.Type, allowConsolidation: false);
        return strategic > 0 || !IsTradeValueUpgrade(state, item)
            ? strategic
            : 500 + item.TradeValue;
    }

    internal static float AssortmentShoppingPriority(
        AssortmentShoppingPriorityState priorityState,
        ItemType type) => ShoppingPriority(priorityState.Needs, type, allowConsolidation: false);

    static float ShoppingPriority(
        StrategicNeeds needs,
        ItemType type,
        bool allowConsolidation)
    {
        if (!needs.CanBuy(type))
            return 0;

        bool waterContainer = AiItemPool.IsWaterContainer(type);
        bool meleeWeapon = needs.CanAcquireAttackWeapon(type);

        // 1. Immediate survival.
        if (type.HealValue > 0 && needs.DoctorPaymentNeeded)
            return 6200 + type.HealValue;
        if (type.FoodValue > 0 && needs.ImmediateFoodNeeded)
            return 6100 + type.FoodValue * 12 + type.TradeValue;
        if (waterContainer && needs.ImmediateWaterNeeded)
            return 6000 + AiItemPool.WaterContainerCapacity(type);

        // 2. The currently active mission.
        if (needs.PlannedSettlementPaymentType == type)
            return 5200 + type.TradeValue;
        if (waterContainer && needs.AttackWaterNeeded)
            return 5100 + AiItemPool.WaterContainerCapacity(type);
        if (AiItemPool.IsHazardProtection(type) && needs.ProvidesMissionProtection(type))
            return 5000 + type.TradeValue;
        if (meleeWeapon && (needs.HasAttackPlan || needs.UrgentWeaponNeeded) &&
            needs.MeleeWeaponStock < needs.MeleeWeaponQuota)
            return 4900 + type.DamageValue;

        // 3. One useful production tool for every unfilled owned camp. Any
        // compatible recipe may advance the shortage; there is no active recipe.
        int productionShortfall = needs.ProductionShortfall(type);
        if (productionShortfall > 0)
            return 3200 + productionShortfall * 40 +
                ProductionTradePriority(type.Production!);
        if (needs.NeedsMaterial(type.ID))
            return 2600 + (needs.CompletesUsefulRecipe(type.ID) ? 300 : 0) +
                needs.MaterialDemandBreadth(type.ID) * 30 + type.TradeValue;
        if (IsPump(type) && needs.PumpStock < needs.PumpQuota)
            return 2400 + (type.ID == "item_industrial_pump" ? 20 : 0);
        if (waterContainer && needs.CampWaterContainerShortfall > 0)
            return (needs.HasCriticalCampWaterContainerShortfall ? 2700 : 1700) +
                AiItemPool.WaterContainerCapacity(type);
        if (waterContainer &&
            needs.PortableWaterCapacity < needs.DesiredAttackWaterContainerCapacity)
            return 1800 + AiItemPool.WaterContainerCapacity(type);

        // 4. Fixed portable reserves.
        if (type.Production != null && needs.PortableProductionReserveNeeded)
            return 1500 + ProductionTradePriority(type.Production);
        if (type.FoodValue > 0 && needs.PortableFood < needs.DesiredPortableFood)
            return 1400 + type.FoodValue * 12 + type.TradeValue;
        if (waterContainer && needs.PortableWaterCapacity < needs.DesiredPortableWaterCapacity)
            return 1300 + AiItemPool.WaterContainerCapacity(type);
        if (meleeWeapon && needs.MeleeWeaponStock < needs.MeleeWeaponQuota)
            return 1200 + type.DamageValue;
        if (AiItemPool.IsHazardProtection(type) &&
            (needs.NeedsProtection(type) || needs.ProtectionStock < needs.ProtectionQuota))
            return 1100 + type.TradeValue;

        // 5. Cargo consolidation is deliberately last.
        return allowConsolidation ? 500 + type.TradeValue : 0;
    }

    internal static bool CanSell(ClassicAiState state, Item item) =>
        CanSell(state, AiTurnContext.For(state).Needs, item);

    static bool CanSell(ClassicAiState state, StrategicNeeds needs, Item item)
    {
        if (needs.PlannedSettlementPaymentType == item.Type)
            return false;
        if (state.Player.Group.Any(character => character.Weapon == item || character.Protection == item))
            return false;
        if (item.Type.Production != null &&
            (needs.NeedsProduction(item.Type) ||
                needs.ProductionToolStock <= needs.ProductionToolQuota))
            return false;
        if (IsPump(item) && needs.PumpStock <= needs.PumpQuota)
            return false;
        if (item.FoodValue > 0)
        {
            int requiredFoodInventory = state.Current.IsCity && state.OwnedCampCount > 0
                ? RecoveryServices.RequiredReturnFoodInventory(state)
                : Math.Max(0, needs.DesiredPortableFood -
                    state.Player.Group.GetFoodReserve());
            if (state.Player.Group.GetFoodInInventory() - item.FoodValue < requiredFoodInventory)
                return false;
        }
        if (AiItemPool.IsWaterContainer(item.Type))
        {
            if (state.Current.IsCity && state.OwnedCampCount > 0 && item.WaterValue > 0 &&
                state.Player.Group.GetWaterInInventory() - item.WaterValue <
                    RecoveryServices.RequiredReturnWaterInventory(state))
                return false;
            int remainingCapacity = Trading.PortableWaterCapacity(state) +
                state.Reserve.TotalWaterContainerCapacity -
                AiItemPool.WaterContainerCapacity(item.Type);
            if (remainingCapacity < needs.DesiredPortableWaterCapacity)
                return false;
        }
        if (AiItemPool.IsHazardProtection(item.Type) &&
            needs.ProtectionStock <= needs.ProtectionQuota)
            return false;
        if (needs.IsPolicyAttackWeapon(item.Type) &&
            needs.MeleeWeaponStock <= needs.MeleeWeaponQuota)
            return false;
        if (Trading.ConstructionMaterials.Contains(item.ID) &&
            needs.MaterialStock(item.ID) <= needs.MaterialQuota(item.ID))
            return false;
        if (item.ID == "item_advice")
            return false;
        return true;
    }

    internal static int SalePriority(Item item)
    {
        if (item.Type.IsClass("useless"))
            return 0;
        if (item.FoodValue > 0)
            return item.FoodValue <= 3 ? 1 : 3;
        if (item.Type.IsClass("protection"))
            return 2;
        return 3;
    }

    internal static float ProductionTradePriority(Production production) =>
        production.Produce.ID switch
        {
            "item_meat" => 240,
            "item_snake" => 200,
            "item_rats" => 100,
            _ => 0
        } + production.Produce.TradeValue + production.Produce.FoodValue;

    internal static bool IsTradeValueUpgrade(ClassicAiState state, Item target)
    {
        if (!AiTurnContext.For(state).Needs.CanBuy(target.Type) ||
            AiItemPool.Accepts(target.Type) || target.TradeValue <= 0)
            return false;
        Item[] lowerValueGoods = state.Player.Group.SelectMany(character => character.Items)
            .Where(item => CanSell(state, item) && item.ID != target.ID &&
                item.TradeValue > 0 && item.TradeValue < target.TradeValue)
            .OrderBy(item => item.TradeValue)
            .ToArray();
        return lowerValueGoods.Length >= 2 &&
            lowerValueGoods.Sum(item => item.TradeValue * TradeBenefit(state)) >= target.TradeValue;
    }

    internal static bool IsStrategicPurchase(ClassicAiState state, Item item) =>
        AiTurnContext.For(state).Needs.IsStrategic(item.Type);

    internal static bool NeedsWeapons(ClassicAiState state)
    {
        if (NeedsTravelOrDefenseWeapons(state))
            return true;

        Location destination = WeaponReserveDestination(state);
        return destination != null &&
            CampManagement.CampStoredWeaponCount(destination) + state.Reserve.MeleeWeaponCount < CampManagement.CampWeaponReserve;
    }

    internal static bool NeedsTravelOrDefenseWeapons(ClassicAiState state)
    {
        IEnumerable<Character> travellers = state.Player.Group.Where(character => !character.IsDead);
        IEnumerable<Character> guards = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && ReinforcementPlanning.IsThreatened(state, location))
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == state.Player));
        return travellers.Any(character => (character.Items.FindBestWeapon()?.DamageValue ?? 0) == 0) ||
            guards.Any(character => (character.Items.FindBestWeapon()?.DamageValue ?? 0) == 0);
    }

    internal static Location WeaponReserveDestination(ClassicAiState state)
    {
        if (state.StrategicTarget != null && !state.StrategicTarget.IsCity)
            return state.StrategicTarget;
        return state.Current.Player == state.Player ? state.Current : null;
    }

    internal static bool NeedsProduction(ClassicAiState state, ItemType type)
    {
        if (type.Production == null)
            return false;
        Location[] camps = state.RootGame.World.Locations.Where(location => location.Player == state.Player).ToArray();
        return camps.Length == 0 || camps.Any(location =>
        {
            if (!location.ValidProductions.Contains(type.Production) ||
                CampEconomy.ProductionToolCount(location, type.Production) >=
                    CampEconomy.DesiredProductionToolCount(state, location, type.Production) ||
                !CampManagement.ShouldPreferProductionAtCamp(state, location))
                return false;
            Production best = location.ValidProductions
                .OrderByDescending(ProductionTradePriority)
                .ThenByDescending(production => production.Produce.FoodValue)
                .FirstOrDefault();
            return best == type.Production;
        });
    }

    internal static bool NeedsDangerProtection(ClassicAiState state, ItemType type)
    {
        bool ownedCampNeed = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location.Danger != null)
            .Any(location => type.GetProtection(location.Danger.Type) != null &&
                location.CampNPC.Any(guard => guard.Player == state.Player &&
                    guard.Items.FindBestProtection(null, location.Danger.Type) == null));
        bool expansionNeed = state.RootGame.World.Locations
            .Where(location => !location.IsCity && location.Player == null && location.Danger != null)
            .Any(location => type.GetProtection(location.Danger.Type) != null &&
                (location.Danger.Type == "radiation"
                    ? !state.Reserve.HasProtectionSuit()
                    : !state.Reserve.HasGasMask()));
        return ownedCampNeed || expansionNeed;
    }

    internal static int DesiredProtectionReserve(ClassicAiState state)
    {
        Location[] camps = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .ToArray();
        if (camps.Length == 0)
            return 0;
        bool hazardCampaign = state.RootGame.World.Locations.Any(location =>
            !location.IsCity && location.Player != state.Player && location.Danger != null);
        if (hazardCampaign)
            return AiPolicy.ForDifficulty(state.Difficulty).AttackGroupSize;
        return camps.Length >= 5 || camps.Any(location => location.Danger != null) ? 2 : 1;
    }

    internal static int GlobalProtectionStock(ClassicAiState state) => state.Reserve.ProtectionCount +
        state.Player.Group.SelectMany(character => character.Items)
            .Count(item => AiItemPool.IsHazardProtection(item.Type));

    internal static bool NeedsBetterWaterContainers(ClassicAiState state, ItemType offered)
    {
        int offeredCapacity = AiItemPool.WaterContainerCapacity(offered);
        if (Trading.PortableWaterCapacity(state) + state.Reserve.TotalWaterContainerCapacity <
            RequiredUnstationedWaterContainerCapacity(state))
            return true;
        return offeredCapacity > state.Reserve.BestWaterContainerCapacity;
    }

    internal static int CampWaterContainerCount(Location camp) =>
        camp.Rooms.SelectMany(room => room.Items)
            .Count(item => AiItemPool.IsWaterContainer(item.Type));

    internal static int CampWaterContainerShortfall(ClassicAiState state) =>
        state.RootGame.World.Locations
            .Where(camp => camp.Player == state.Player && !camp.IsCity)
            .Sum(camp => System.Math.Max(0,
                DesiredCampWaterContainerCount(camp) - CampWaterContainerCount(camp)));

    internal static int RequiredUnstationedWaterContainerCapacity(ClassicAiState state) =>
        DesiredWaterContainerCapacity(state) + CampWaterContainerShortfall(state) * 3;

    internal static float ConstructionMaterialPriority(ClassicAiState state, string itemId)
    {
        StrategicNeeds needs = AiTurnContext.For(state).Needs;
        if (!needs.NeedsMaterial(itemId))
            return 0;
        return 2600 + (needs.CompletesUsefulRecipe(itemId) ? 300 : 0) +
            needs.MaterialDemandBreadth(itemId) * 30;
    }

    internal static int DesiredMaterialStock(ClassicAiState state, string itemId) =>
        AiTurnContext.For(state).Needs.MaterialQuota(itemId);

    internal static int MissingProductionToolCount(ClassicAiState state, string productId) =>
        state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && location.Danger == null)
            .SelectMany(camp => camp.ValidProductions
                .Where(production => production.Produce.ID == productId)
                .Select(production => new { Camp = camp, Production = production }))
            .Sum(entry => System.Math.Max(0,
                CampEconomy.DesiredProductionToolCount(state, entry.Camp, entry.Production) -
                CampEconomy.ProductionToolCount(entry.Camp, entry.Production)));

    internal static bool CanPrepareProductionInAdvance(ClassicAiState state) =>
        state.OwnedCampCount == 0 ||
        MissingProductionToolCount(state, "item_meat") +
        MissingProductionToolCount(state, "item_rats") +
        MissingProductionToolCount(state, "item_snake") == 0;

    internal static int PortableMaterialCount(ClassicAiState state, string itemId) =>
        state.Reserve.GetConstructionMaterialCount(itemId) +
        state.Player.Group.SelectMany(character => character.Items).Count(item => item.ID == itemId);

}
