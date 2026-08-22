using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class TradeTask
{
    internal const int DesiredCampWaterContainerCount = 2;

    internal sealed class AssortmentShoppingPriorityState
    {
        public ClassicAiState State { get; }
        public bool EarlyEconomy { get; }
        public bool ProductionEconomyNeeded { get; }
        public bool ProductionUpgradeNeeded { get; }
        public bool FirstAdvancedTrapNeeded { get; }
        public ItemType PlannedSettlementPaymentType { get; }
        public bool StrategicSnakeTrapNeeded { get; }
        public bool SavingForSnakeTrap { get; }
        public bool DoctorPaymentNeeded { get; }
        public bool CriticalWaterWaypointUpgradeNeeded { get; }
        public bool CanPrepareExpansionProduction { get; }
        public bool WeaponsNeeded { get; }
        public int GlobalProtectionCount { get; }
        public int ProtectionReserve { get; }
        public bool AnyPumpNeeded { get; }
        public bool CriticalPumpNeeded { get; }
        public float BestEmpireImprovement { get; }
        public int PortableFood { get; }
        public int DesiredPortableFood { get; }
        public int AvailableWaterContainerCapacity { get; }
        public int RequiredWaterContainerCapacity { get; }
        public int BestWaterContainerCapacity { get; }
        public bool HasAttackPlan { get; }

        public AssortmentShoppingPriorityState(ClassicAiState state)
        {
            State = state;
            EarlyEconomy = state.OwnedCampCount < 3;
            ProductionEconomyNeeded = ExpansionTask.ShouldPrioritizeEconomicGrowth(state);
            ProductionUpgradeNeeded = EconomicReturn.BestEmpireProductionImprovement(state) > 0.01f;
            FirstAdvancedTrapNeeded = !EconomicSupport.HasAdvancedTrap(state);
            PlannedSettlementPaymentType = RecruitmentTask.PlannedFutureSettlementPaymentType(state);
            StrategicSnakeTrapNeeded = HasStrategicSnakeTrapNeed(state);
            SavingForSnakeTrap = EconomicSupport.IsSavingForSnakeTrap(state);
            DoctorPaymentNeeded = RecoveryServices.NeedsDoctorPayment(state);
            CriticalWaterWaypointUpgradeNeeded = NeedsCriticalWaterWaypointUpgrade(state);
            CanPrepareExpansionProduction = CanPrepareProductionInAdvance(state) &&
                ExpansionTask.NeedsExpansionTool(state);
            WeaponsNeeded = NeedsWeapons(state);
            GlobalProtectionCount = GlobalProtectionStock(state);
            ProtectionReserve = DesiredProtectionReserve(state);
            AnyPumpNeeded = NeedsAnyPump(state);
            CriticalPumpNeeded = NeedsCriticalPump(state);
            BestEmpireImprovement = EconomicReturn.BestEmpireImprovement(state);
            PortableFood = TradeTask.PortableFoodSupply(state);
            DesiredPortableFood = TradeTask.DesiredPortableFood(state);
            AvailableWaterContainerCapacity = TradeTask.PortableWaterCapacity(state) +
                state.Pool.TotalWaterContainerCapacity;
            RequiredWaterContainerCapacity = RequiredUnstationedWaterContainerCapacity(state);
            BestWaterContainerCapacity = state.Pool.BestWaterContainerCapacity;
            HasAttackPlan = state.HasAttackPlan;
        }
    }

    internal static float ShoppingPriority(ClassicAiState state, Item item)
    {
        bool earlyEconomy = state.OwnedCampCount < 3;
        bool productionEconomyNeeded = ExpansionTask.ShouldPrioritizeEconomicGrowth(state);
        bool productionUpgradeNeeded = EconomicReturn.BestEmpireProductionImprovement(state) > 0.01f;
        bool firstAdvancedTrapNeeded = !EconomicSupport.HasAdvancedTrap(state);
        if (RecruitmentTask.PlannedFutureSettlementPaymentType(state) == item.Type)
            return 5100 + item.TradeValue;
        if (item.ID == "item_snake_trap" && HasStrategicSnakeTrapNeed(state))
            return 5000;
        if (EconomicSupport.IsSavingForSnakeTrap(state) && item.FoodValue == 0 &&
            !AiItemPool.IsWaterContainer(item.Type))
            return 0;
        if (item.HealValue > 0 && RecoveryServices.NeedsDoctorPayment(state))
            return 6000 + item.HealValue;
        if (NeedsCriticalWaterWaypointUpgrade(state))
        {
            if (IsPump(item))
                return 4900 + (item.ID == "item_industrial_pump" ? 20 : 0);
            if (AiItemPool.IsWaterContainer(item.Type))
                return 4800 + AiItemPool.WaterContainerCapacity(item.Type);
        }
        if (item.Type.Production != null)
        {
            int ownedProductionDemand = MissingProductionToolCount(
                state, item.Type.Production.Produce.ID);
            if (ownedProductionDemand > 0)
                return 1700 + (firstAdvancedTrapNeeded ? 500 : 0) + ownedProductionDemand * 100 +
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
            return materialPriority + (firstAdvancedTrapNeeded ? 300 : 0) + item.TradeValue;

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
            return (state.HasAttackPlan ? 1150 : 1050) +
                AiItemPool.WaterContainerCapacity(item.Type);
        if (IsTradeValueUpgrade(state, item))
            return 500 + item.TradeValue;
        return 0;
    }

    internal static float AssortmentShoppingPriority(
        AssortmentShoppingPriorityState priorityState,
        ItemType type)
    {
        ClassicAiState state = priorityState.State;
        Production? production = type.Production;
        float productionPriority = production == null ? 0 : ProductionTradePriority(production);
        bool isPump = IsPump(type);
        bool isWaterContainer = AiItemPool.IsWaterContainer(type);

        if (priorityState.PlannedSettlementPaymentType == type)
            return 5100 + type.TradeValue;
        if (type.ID == "item_snake_trap" && priorityState.StrategicSnakeTrapNeeded)
            return 5000;
        if (priorityState.SavingForSnakeTrap && type.FoodValue == 0 && !isWaterContainer)
            return 0;
        if (type.HealValue > 0 && priorityState.DoctorPaymentNeeded)
            return 6000 + type.HealValue;
        if (priorityState.CriticalWaterWaypointUpgradeNeeded)
        {
            if (isPump)
                return 4900 + (type.ID == "item_industrial_pump" ? 20 : 0);
            if (isWaterContainer)
                return 4800 + AiItemPool.WaterContainerCapacity(type);
        }
        if (production != null)
        {
            int ownedProductionDemand = MissingProductionToolCount(
                state, production.Produce.ID);
            if (ownedProductionDemand > 0)
                return 1700 + (priorityState.FirstAdvancedTrapNeeded ? 500 : 0) +
                    ownedProductionDemand * 100 + productionPriority +
                    EconomicReturn.ProductionToolReturn(state, production) * 120;
        }
        if (priorityState.CanPrepareExpansionProduction && production != null &&
            IsUsefulForNeutralExpansion(state, production))
            return 1600 + productionPriority;
        if (production != null && NeedsProduction(state, type))
            return 1500 + productionPriority;

        float materialPriority = ConstructionMaterialPriority(state, type.ID);
        if (materialPriority > 0)
            return materialPriority + (priorityState.FirstAdvancedTrapNeeded ? 300 : 0) +
                type.TradeValue;
        if (type.DamageValue > 0 && !AiItemPool.IsFirearm(type) && priorityState.WeaponsNeeded)
            return 920 + type.DamageValue;
        if (AiItemPool.IsHazardProtection(type))
        {
            bool dangerProtectionNeeded = NeedsDangerProtection(state, type);
            if (dangerProtectionNeeded ||
                priorityState.GlobalProtectionCount < priorityState.ProtectionReserve)
                return (dangerProtectionNeeded ? 900 : priorityState.ProductionEconomyNeeded ? 560 : 800) +
                type.TradeValue;
        }
        if (isPump && priorityState.AnyPumpNeeded &&
            (!priorityState.ProductionUpgradeNeeded || priorityState.CriticalPumpNeeded))
            return (priorityState.ProductionUpgradeNeeded ? 540 : priorityState.EarlyEconomy ? 780 : 720) +
                (type.ID == "item_industrial_pump" ? 20 : 0) +
                priorityState.BestEmpireImprovement * 100;
        if (type.FoodValue > 0 && priorityState.PortableFood < priorityState.DesiredPortableFood)
            return 900 + type.FoodValue * 12 + type.TradeValue;
        if (isWaterContainer)
        {
            int capacity = AiItemPool.WaterContainerCapacity(type);
            if (priorityState.AvailableWaterContainerCapacity <
                    priorityState.RequiredWaterContainerCapacity ||
                capacity > priorityState.BestWaterContainerCapacity)
                return (priorityState.HasAttackPlan ? 1150 : 1050) + capacity;
        }
        return 0;
    }

    internal static bool CanSell(ClassicAiState state, Item item)
    {
        if (RecruitmentTask.PlannedFutureSettlementPaymentType(state) == item.Type)
            return false;
        if (state.Player.Group.Any(character => character.Weapon == item || character.Protection == item))
            return false;
        // A working pump encountered before the low-water camp is claimed is a
        // scarce strategic asset, not generic barter filler.
        if ((IsPump(item) && HasForeseeablePumpNeed(state)) ||
            (item.Type.Production != null && state.OwnedCampCount == 0))
            return false;
        if (AiItemPool.IsWaterContainer(item.Type) &&
            NeedsCriticalWaterWaypointUpgrade(state))
            return false;
        if (item.FoodValue > 0)
        {
            int requiredFoodInventory = state.Current.IsCity && state.OwnedCampCount > 0
                ? RecoveryServices.RequiredReturnFoodInventory(state)
                : Math.Max(0, TradeTask.DesiredPortableFood(state) -
                    state.Player.Group.GetFoodReserve());
            if (state.Player.Group.GetFoodInInventory() - item.FoodValue < requiredFoodInventory)
                return false;
        }
        if (item.Type.Production != null && HasNeutralExpansionOpportunity(state) &&
            state.Pool.ProductionToolCount == 0 &&
            state.Player.Group.SelectMany(character => character.Items)
                .Count(candidate => candidate.Type.Production != null) <= 1)
            return false;
        if (AiItemPool.IsWaterContainer(item.Type))
        {
            if (state.Current.IsCity && state.OwnedCampCount > 0 && item.WaterValue > 0 &&
                state.Player.Group.GetWaterInInventory() - item.WaterValue <
                    RecoveryServices.RequiredReturnWaterInventory(state))
                return false;
            int remainingCapacity = TradeTask.PortableWaterCapacity(state) +
                state.Pool.TotalWaterContainerCapacity -
                AiItemPool.WaterContainerCapacity(item.Type);
            if (remainingCapacity < RequiredUnstationedWaterContainerCapacity(state))
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
        RecruitmentTask.PlannedFutureSettlementPaymentType(state) == item.Type ||
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
        if (TradeTask.PortableWaterCapacity(state) + state.Pool.TotalWaterContainerCapacity <
            RequiredUnstationedWaterContainerCapacity(state))
            return true;
        return offeredCapacity > state.Pool.BestWaterContainerCapacity;
    }

    internal static int CampWaterContainerCount(Location camp) =>
        camp.Rooms.SelectMany(room => room.Items)
            .Count(item => AiItemPool.IsWaterContainer(item.Type));

    internal static int CampWaterContainerShortfall(ClassicAiState state) =>
        state.RootGame.World.Locations
            .Where(camp => camp.Player == state.Player)
            .Sum(camp => System.Math.Max(0,
                DesiredCampWaterContainerCount - CampWaterContainerCount(camp)));

    internal static int RequiredUnstationedWaterContainerCapacity(ClassicAiState state) =>
        DesiredWaterContainerCapacity(state) + CampWaterContainerShortfall(state) * 3;

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
