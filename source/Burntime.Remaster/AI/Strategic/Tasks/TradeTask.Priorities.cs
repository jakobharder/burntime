using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class TradeTask
{
    internal static float ShoppingPriority(ClassicAiState state, Item item)
    {
        bool earlyEconomy = state.OwnedCampCount < 3;
        bool productionEconomyNeeded = ExpansionTask.ShouldPrioritizeEconomicGrowth(state);
        bool productionUpgradeNeeded = EconomicReturn.BestEmpireProductionImprovement(state) > 0.01f;
        if (item.Type.Production != null)
        {
            int ownedProductionDemand = MissingProductionToolCount(
                state, item.Type.Production.Produce.ID);
            if (ownedProductionDemand > 0)
                return 1700 + ownedProductionDemand * 100 +
                    ProductionTradePriority(item.Type.Production) +
                    EconomicReturn.ProductionToolReturn(state, item.Type.Production) * 120;
        }
        if (CanPrepareProductionInAdvance(state) && ExpansionTask.NeedsExpansionTool(state) &&
            item.Type.Production != null &&
            IsUsefulForNeutralExpansion(state, item.Type.Production))
            return 1600 + ProductionTradePriority(item.Type.Production);
        if (item.Type.Production != null && NeedsProduction(state, item.Type))
        {
            // A complete useful trap always outranks individual recipe parts.
            // Components are the fallback when the finished tool is not offered.
            return 1500 + ProductionTradePriority(item.Type.Production);
        }

        // Limiting trap components outrank secondary equipment. Buying another
        // knife or pump must not postpone a nearly complete production upgrade.
        float materialPriority = ConstructionMaterialPriority(state, item.ID);
        if (materialPriority > 0)
            return materialPriority + item.TradeValue;

        if (item.DamageValue > 0 && !AiItemPool.IsFirearm(item.Type) && NeedsWeapons(state))
            return 920 + item.DamageValue;
        if (AiItemPool.IsHazardProtection(item.Type) &&
            (NeedsDangerProtection(state, item.Type) || GlobalProtectionStock(state) < DesiredProtectionReserve(state)))
            return (NeedsDangerProtection(state, item.Type) ? 900 : productionEconomyNeeded ? 560 : 800) +
                item.TradeValue;
        if (IsPump(item) && NeedsAnyPump(state) &&
            (!productionUpgradeNeeded || NeedsCriticalPump(state)))
            return (productionUpgradeNeeded ? 540 : earlyEconomy ? 780 : 720) +
                (item.ID == "item_industrial_pump" ? 20 : 0) +
                EconomicReturn.BestEmpireImprovement(state) * 100;

        int portableFood = TradeTask.PortableFoodSupply(state);
        if (item.FoodValue > 0 && portableFood < TradeTask.DesiredPortableFood(state))
            return 900 +
                item.FoodValue * 12 + item.TradeValue;

        int lowestFood = state.Player.Group.SelectMany(character => character.Items)
            .Where(candidate => candidate.FoodValue > 0)
            .Select(candidate => candidate.FoodValue)
            .DefaultIfEmpty(0)
            .Min();
        int lowerFoodItems = state.Player.Group.SelectMany(character => character.Items)
            .Count(candidate => candidate.FoodValue > 0 && candidate.FoodValue < item.FoodValue);
        if (item.FoodValue > lowestFood && lowestFood > 0 &&
            (state.Player.Group.GetFreeSlotCount() <= 3 || lowerFoodItems >= 2))
        {
            return 640 + (item.FoodValue - lowestFood) * 12 + item.TradeValue;
        }
        if (AiItemPool.IsWaterContainer(item.Type) && NeedsBetterWaterContainers(state, item.Type))
            return (state.HasAttackPlan ? 760 : 600) +
                AiItemPool.WaterContainerCapacity(item.Type);
        if (IsTradeValueUpgrade(state, item))
            return 500 + item.TradeValue;
        return 0;
    }

    internal static float AssortmentShoppingPriority(ClassicAiState state, ItemType type)
    {
        bool earlyEconomy = state.OwnedCampCount < 3;
        bool productionEconomyNeeded = ExpansionTask.ShouldPrioritizeEconomicGrowth(state);
        bool productionUpgradeNeeded = EconomicReturn.BestEmpireProductionImprovement(state) > 0.01f;
        if (type.Production != null)
        {
            int ownedProductionDemand = MissingProductionToolCount(
                state, type.Production.Produce.ID);
            if (ownedProductionDemand > 0)
                return 1700 + ownedProductionDemand * 100 +
                    ProductionTradePriority(type.Production) +
                    EconomicReturn.ProductionToolReturn(state, type.Production) * 120;
        }
        if (CanPrepareProductionInAdvance(state) && ExpansionTask.NeedsExpansionTool(state) &&
            type.Production != null &&
            IsUsefulForNeutralExpansion(state, type.Production))
            return 1600 + ProductionTradePriority(type.Production);
        if (type.Production != null && NeedsProduction(state, type))
            return 1500 + ProductionTradePriority(type.Production);

        float materialPriority = ConstructionMaterialPriority(state, type.ID);
        if (materialPriority > 0)
            return materialPriority + type.TradeValue;
        if (type.DamageValue > 0 && !AiItemPool.IsFirearm(type) && NeedsWeapons(state))
            return 920 + type.DamageValue;
        if (AiItemPool.IsHazardProtection(type) &&
            (NeedsDangerProtection(state, type) || GlobalProtectionStock(state) < DesiredProtectionReserve(state)))
            return (NeedsDangerProtection(state, type) ? 900 : productionEconomyNeeded ? 560 : 800) +
                type.TradeValue;
        if (IsPump(type) && NeedsAnyPump(state) &&
            (!productionUpgradeNeeded || NeedsCriticalPump(state)))
            return (productionUpgradeNeeded ? 540 : earlyEconomy ? 780 : 720) +
                (type.ID == "item_industrial_pump" ? 20 : 0) +
                EconomicReturn.BestEmpireImprovement(state) * 100;
        if (type.FoodValue > 0 && TradeTask.PortableFoodSupply(state) < TradeTask.DesiredPortableFood(state))
            return 900 + type.FoodValue * 12 + type.TradeValue;
        if (AiItemPool.IsWaterContainer(type) && NeedsBetterWaterContainers(state, type))
            return (state.HasAttackPlan ? 760 : 600) + AiItemPool.WaterContainerCapacity(type);
        return 0;
    }

    internal static bool CanSell(ClassicAiState state, Item item)
    {
        if (state.Player.Group.Any(character => character.Weapon == item || character.Protection == item))
            return false;
        if ((IsPump(item) && NeedsAnyPump(state)) ||
            (item.Type.Production != null && state.OwnedCampCount == 0))
            return false;
        if (item.FoodValue > 0 && TradeTask.PortableFoodSupply(state) - item.FoodValue <
            TradeTask.DesiredPortableFood(state))
            return false;
        if (item.Type.Production != null && HasNeutralExpansionOpportunity(state) &&
            state.Pool.ProductionToolCount == 0 &&
            state.Player.Group.SelectMany(character => character.Items)
                .Count(candidate => candidate.Type.Production != null) <= 1)
            return false;
        if (AiItemPool.IsWaterContainer(item.Type))
        {
            int remainingCapacity = TradeTask.PortableWaterSupply(state) +
                state.Pool.TotalWaterContainerCapacity -
                AiItemPool.WaterContainerCapacity(item.Type);
            if (remainingCapacity < TradeTask.DesiredPortableWaterCapacity(state))
                return false;
        }
        if (AiItemPool.IsHazardProtection(item.Type) &&
            GlobalProtectionStock(state) <= DesiredProtectionReserve(state))
            return false;
        if (item.Type.IsClass("weapon") && !AiItemPool.IsFirearm(item.Type) && NeedsWeapons(state))
            return false;
        if (TradeTask.ConstructionMaterials.Contains(item.ID) &&
            PortableMaterialCount(state, item.ID) <= DesiredMaterialStock(state, item.ID))
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
        if ((AiItemPool.Accepts(target.Type) && !AiItemPool.IsFirearm(target.Type)) || target.TradeValue <= 0)
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
        item.Type.Production != null ||
        (item.FoodValue > 0 && TradeTask.PortableFoodSupply(state) < TradeTask.DesiredPortableFood(state)) ||
        (!AiItemPool.IsFirearm(item.Type) && item.DamageValue > 0 && NeedsWeapons(state)) ||
        (AiItemPool.IsHazardProtection(item.Type) &&
            (NeedsDangerProtection(state, item.Type) || GlobalProtectionStock(state) < DesiredProtectionReserve(state))) ||
        (IsPump(item) && NeedsAnyPump(state)) ||
        ConstructionMaterialPriority(state, item.ID) > 0;

    internal static bool NeedsWeapons(ClassicAiState state)
    {
        if (NeedsTravelOrDefenseWeapons(state))
            return true;

        Location destination = WeaponReserveDestination(state);
        return destination != null &&
            LocalOpportunities.CampStoredWeaponCount(destination) + state.Pool.MeleeWeaponCount < LocalOpportunities.CampWeaponReserve;
    }

    internal static bool NeedsTravelOrDefenseWeapons(ClassicAiState state)
    {
        IEnumerable<Character> travellers = state.Player.Group.Where(character => !character.IsDead);
        IEnumerable<Character> guards = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player && ReinforcementTask.IsThreatened(state, location))
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
                !LocalOpportunities.ShouldPreferProductionAtCamp(state, location))
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
                    ? !state.Pool.HasProtectionSuit()
                    : !state.Pool.HasGasMask()));
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
            return state.Player.Group.Count;
        return camps.Length >= 5 || camps.Any(location => location.Danger != null) ? 2 : 1;
    }

    internal static int GlobalProtectionStock(ClassicAiState state) => state.Pool.ProtectionCount +
        state.Player.Group.SelectMany(character => character.Items)
            .Count(item => AiItemPool.IsHazardProtection(item.Type));

    internal static bool NeedsBetterWaterContainers(ClassicAiState state, ItemType offered)
    {
        int offeredCapacity = AiItemPool.WaterContainerCapacity(offered);
        if (TradeTask.PortableWaterSupply(state) + state.Pool.TotalWaterContainerCapacity <
            TradeTask.DesiredPortableWaterCapacity(state))
            return true;
        IEnumerable<Character> guards = state.RootGame.World.Locations
            .Where(location => location.Player == state.Player)
            .SelectMany(location => location.CampNPC.Where(npc => npc.Player == state.Player));
        return guards.Any(npc => !HasWaterContainer(npc)) ||
            offeredCapacity > state.Pool.BestWaterContainerCapacity;
    }

    internal static float ConstructionMaterialPriority(ClassicAiState state, string itemId)
    {
        if (!TradeTask.ConstructionMaterials.Contains(itemId))
            return 0;

        ConstructionOpportunity[] opportunities = UsefulConstructionOpportunities(state)
            .Where(opportunity => opportunity.Materials.Contains(itemId))
            .ToArray();
        if (opportunities.Length == 0)
            return 0;

        int stock = PortableMaterialCount(state, itemId);
        int desired = DesiredMaterialStock(state, itemId);
        if (stock >= desired)
            return 0;

        ConstructionOpportunity[] limitingOpportunities = opportunities
            .Where(opportunity => stock <= opportunity.Materials
                .Min(component => PortableMaterialCount(state, component)))
            .ToArray();
        if (limitingOpportunities.Length == 0)
            return 0;

        if (state.Pool.GetConstructionMaterialCount(itemId) > 0)
        {
            // Once the immediate recipe reserve is covered, build a small physical
            // pipeline of common rat-trap parts without treating one exact recipe as
            // a shopping mission.
            return 650 + limitingOpportunities.Max(opportunity => opportunity.EconomicValue) +
                (desired - stock) * 20;
        }

        float best = limitingOpportunities.Max(opportunity =>
        {
            int missing = opportunity.Materials.Count(component => !HasConstructionComponent(state, component));
            float completion = missing switch
            {
                <= 1 => 1250,
                2 => 980,
                _ => 820
            };
            return completion + opportunity.EconomicValue;
        });
        return best + System.Math.Max(0, limitingOpportunities.Length - 1) * 25;
    }

    internal static int DesiredMaterialStock(ClassicAiState state, string itemId)
    {
        if (!ExpansionTask.ShouldPrioritizeEconomicGrowth(state))
            return 1;

        int ratTrapNeeds = MissingProductionToolCount(state, "item_rats");
        int meatTrapNeeds = MissingProductionToolCount(state, "item_meat");

        // Scale the opportunity-buy pipeline with actual missing traps. Caps avoid
        // turning components into unlimited hidden stock while still allowing
        // several complete recipes to be accumulated across ordinary city visits.
        return itemId switch
        {
            "item_wire" => System.Math.Clamp(ratTrapNeeds + meatTrapNeeds, 1, 4),
            "item_woodpile" or "item_screws" => System.Math.Clamp(ratTrapNeeds, 1, 4),
            "item_spring" or "item_tin" => System.Math.Clamp(meatTrapNeeds, 1, 3),
            _ => 1
        };
    }

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
        state.Pool.GetConstructionMaterialCount(itemId) +
        state.Player.Group.SelectMany(character => character.Items).Count(item => item.ID == itemId);

}
