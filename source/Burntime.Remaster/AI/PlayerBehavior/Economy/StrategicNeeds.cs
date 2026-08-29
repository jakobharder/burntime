using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// The economic-needs view owned by <see cref="AiTurnContext"/>. It is built at
/// the beginning of an AI turn and refreshed after barter changes inventory.
/// This bounds planning work while keeping economic demand internally coherent.
/// </summary>
internal sealed class StrategicNeeds
{
    internal const int MaximumPurchasedItemsPerType = 5;

    static readonly Dictionary<string, string[]> ProductionRecipes = new()
    {
        ["item_meat"] = new[] { "item_spring", "item_tin", "item_wire" },
        ["item_rats"] = new[] { "item_wire", "item_woodpile", "item_screws" }
    };

    static readonly string[][] PumpRecipes =
    {
        new[] { "item_broken_pump", "item_rags", "item_hose" },
        new[] { "item_spare_parts", "item_iron_pipe", "item_rags", "item_hose" }
    };
    static readonly string[] ProtectionMaterials =
        { "item_gas_mask", "item_gloves", "item_protective_overall", "item_boots" };

    readonly Dictionary<string, int> productionDemand = new();
    readonly Dictionary<string, int> productionStock = new();
    readonly Dictionary<string, int> materialQuota = new();
    readonly Dictionary<string, int> materialStock = new();
    readonly Dictionary<string, int> globalItemStock = new();
    readonly List<HashSet<string>> usefulRecipes = new();
    readonly HashSet<string> requiredHazards = new();
    readonly int pitchforkLimit;
    readonly int pitchforkStock;

    public ItemType? PlannedSettlementPaymentType { get; }
    public bool DoctorPaymentNeeded { get; }
    public bool HasAttackPlan { get; }
    public bool AttackWaterNeeded { get; }
    public string? MissionHazard { get; }
    public bool ImmediateFoodNeeded { get; }
    public bool ImmediateWaterNeeded { get; }
    public int PortableFood { get; }
    public int DesiredPortableFood { get; }
    public int PortableWaterCapacity { get; }
    public int DesiredPortableWaterCapacity { get; }
    public int DesiredAttackWaterContainerCapacity { get; }
    public int CampWaterContainerShortfall { get; }
    public bool HasCriticalCampWaterContainerShortfall { get; }
    public int MeleeWeaponStock { get; }
    public int MeleeWeaponQuota { get; }
    public bool UrgentWeaponNeeded { get; }
    public int ProtectionStock { get; }
    public int ProtectionQuota { get; }
    public int PumpStock { get; }
    public int PumpQuota { get; }
    public int ProductionToolStock { get; }
    public int ProductionToolQuota { get; }
    public bool HasOwnedProductionShortfall { get; private set; }
    public bool PortableProductionReserveNeeded { get; private set; }
    public bool HasCompleteUsefulRecipe => usefulRecipes.Any(recipe =>
        recipe.All(component => MaterialStock(component) > 0));

    public StrategicNeeds(ClassicAiState state)
    {
        Player player = state.Player;
        Item[] portableItems = player.Group.SelectMany(character => character.Items).ToArray();
        (ItemType Type, int Count)[] poolItems = state.Reserve.GetContents().ToArray();

        PlannedSettlementPaymentType = Recruitment.PlannedFutureSettlementPaymentType(state);
        DoctorPaymentNeeded = RecoveryServices.NeedsDoctorPayment(state);
        HasAttackPlan = state.HasAttackPlan;
        AttackWaterNeeded = state.HasAttackPlan && Trading.NeedsAttackWaterPreparation(state);
        MissionHazard = state.StrategicTarget?.Danger?.Type;
        ImmediateFoodNeeded = player.Group.Any(character => character.Food <= 3);
        ImmediateWaterNeeded = player.Group.Any(character => character.Water <= 2);
        PortableFood = Trading.PortableFoodSupply(state);
        DesiredPortableFood = Trading.DesiredPortableFood(state);
        PortableWaterCapacity = Trading.PortableWaterCapacity(state) +
            state.Reserve.TotalWaterContainerCapacity;
        DesiredPortableWaterCapacity = Trading.DesiredPortableWaterCapacity(state);
        AiPolicy policy = AiPolicy.ForDifficulty(state.Difficulty);
        pitchforkLimit = policy.PitchforkLimit;
        DesiredAttackWaterContainerCapacity =
            Trading.DesiredWaterContainerCapacity(policy.AttackGroupSize);
        CampWaterContainerShortfall = Trading.CampWaterContainerShortfall(state);
        HasCriticalCampWaterContainerShortfall = state.RootGame.World.Locations.Any(camp =>
            camp.Player == player && !camp.IsCity &&
            CampEconomy.IsTravelWaterBottleneck(camp) &&
            Trading.CampWaterContainerCount(camp) <
                Trading.DesiredCampWaterContainerCount(camp));

        int ordinaryMeleeWeapons = portableItems.Count(item =>
                IsMeleeWeapon(item) && item.ID != "item_pitchfork") +
            poolItems.Where(entry => entry.Type.ID != "item_pitchfork" &&
                    IsMeleeWeapon(entry.Type))
                .Sum(entry => entry.Count);
        pitchforkStock = portableItems.Count(item => item.ID == "item_pitchfork") +
            poolItems.Where(entry => entry.Type.ID == "item_pitchfork")
                .Sum(entry => entry.Count);
        MeleeWeaponStock = ordinaryMeleeWeapons + Math.Min(pitchforkStock, pitchforkLimit);
        UrgentWeaponNeeded = state.RootGame.World.Day >= 100 &&
            !AttackPlanning.HasGroupWeapon(player);
        // Prepare durable attack equipment during ordinary trade instead of
        // starting from the current travel group only after choosing a target.
        MeleeWeaponQuota = policy.AttackGroupSize;
        ProtectionStock = portableItems.Count(item => AiItemPool.IsHazardProtection(item.Type)) +
            state.Reserve.ProtectionCount;
        ProtectionQuota = Trading.DesiredProtectionReserve(state);
        PumpStock = portableItems.Count(Trading.IsPump);

        foreach (Item item in portableItems)
        {
            if (item.Type.Production != null)
                Add(productionStock, item.Type.Production.Produce.ID);
            if (Trading.ConstructionMaterials.Contains(item.ID))
                Add(materialStock, item.ID);
        }
        foreach ((ItemType type, int count) in poolItems)
        {
            if (type.Production != null)
                Add(productionStock, type.Production.Produce.ID, count);
            if (Trading.ConstructionMaterials.Contains(type.ID))
                Add(materialStock, type.ID, count);
        }

        Location[] ownedCamps = state.RootGame.World.Locations
            .Where(location => location.Player == player && !location.IsCity)
            .ToArray();
        foreach (Item item in portableItems)
            Add(globalItemStock, item.ID);
        foreach ((ItemType type, int count) in poolItems)
            Add(globalItemStock, type.ID, count);
        foreach (Item item in ownedCamps.SelectMany(camp => camp.Rooms
            .SelectMany(room => room.Items)
            .Concat(camp.CampNPC
                .Where(character => character.Player == player &&
                    !player.Group.Contains(character))
                .SelectMany(character => character.Items))
            .Concat(camp.Items)))
            Add(globalItemStock, item.ID);

        List<(HashSet<string> Products, List<string[]> Recipes)> unfilledCamps = new();
        foreach (Location camp in ownedCamps)
        {
            if (Trading.NeedsPump(camp))
            {
                PumpQuota++;
                AddOpportunity(PumpRecipes);
            }

            if (camp.Danger != null)
            {
                if (camp.CampNPC.Any(guard => guard.Player == player &&
                    guard.Items.FindBestProtection(null, camp.Danger.Type) == null))
                {
                    requiredHazards.Add(camp.Danger.Type);
                    AddOpportunity(new[] { ProtectionMaterials });
                }
                continue;
            }
            if (!CampManagement.ShouldPreferProductionAtCamp(state, camp))
                continue;

            Production[] useful = camp.ValidProductions
                .Where(production => Trading.ProductionTradePriority(production) > 0)
                .ToArray();
            bool hasTool = useful.Any(production =>
                CampEconomy.ProductionToolCount(camp, production) > 0);
            if (hasTool)
                continue;

            HashSet<string> campProducts = new();
            List<string[]> campRecipes = new();
            foreach (Production production in useful)
            {
                string productId = production.Produce.ID;
                campProducts.Add(productId);
                if (ProductionRecipes.TryGetValue(productId, out string[]? recipe))
                    campRecipes.Add(recipe);
            }
            if (campProducts.Count > 0)
                unfilledCamps.Add((campProducts, campRecipes));
        }

        // One portable tool can fill one compatible camp. Allocate scarce,
        // specialized tools first so a multi-production camp does not create
        // independent meat, rat, and snake quotas.
        foreach ((string productId, int count) in productionStock
            .OrderBy(entry => unfilledCamps.Count(camp => camp.Products.Contains(entry.Key))))
        {
            for (int copy = 0; copy < count; copy++)
            {
                var camp = unfilledCamps
                    .Where(candidate => candidate.Products.Contains(productId))
                    .OrderBy(candidate => candidate.Products.Count)
                    .FirstOrDefault();
                if (camp.Products == null)
                    break;
                unfilledCamps.Remove(camp);
            }
        }
        HasOwnedProductionShortfall = unfilledCamps.Count > 0;
        foreach (var camp in unfilledCamps)
        {
            foreach (string productId in camp.Products)
                Add(productionDemand, productId);
            if (camp.Recipes.Count > 0)
                AddOpportunity(camp.Recipes);
        }

        foreach (Location location in state.RootGame.World.Locations.Where(location =>
            !location.IsCity && location.Player == null && location.Danger != null))
            requiredHazards.Add(location.Danger.Type);

        int portableProductionTools = productionStock.Values.Sum();
        ProductionToolStock = portableProductionTools;
        ProductionToolQuota = state.RootGame.World.Locations.Any(location => !location.IsCity &&
                location.Player == null && location.ValidProductions.Any(production =>
                    Trading.ProductionTradePriority(production) > 0)) ? 1 : 0;
        PortableProductionReserveNeeded = ProductionToolStock < ProductionToolQuota;
        if (PortableProductionReserveNeeded && !HasOwnedProductionShortfall)
        {
            List<string[]> reserveRecipes = new();
            foreach (string productId in state.RootGame.World.Locations
                .Where(location => !location.IsCity && location.Player == null)
                .SelectMany(location => location.ValidProductions)
                .Where(production => Trading.ProductionTradePriority(production) > 0)
                .Select(production => production.Produce.ID)
                .Distinct())
            {
                if (ProductionRecipes.TryGetValue(productId, out string[]? recipe))
                    reserveRecipes.Add(recipe);
            }
            if (reserveRecipes.Count > 0)
                AddOpportunity(reserveRecipes);
        }
    }

    public bool NeedsProduction(ItemType type)
    {
        if (type.Production == null)
            return false;
        string productId = type.Production.Produce.ID;
        return Demand(productionDemand, productId) > 0 ||
            PortableProductionReserveNeeded;
    }

    public int ProductionShortfall(ItemType type)
    {
        if (type.Production == null)
            return 0;
        string productId = type.Production.Produce.ID;
        return Demand(productionDemand, productId);
    }

    public bool NeedsMaterial(string itemId) =>
        MaterialStock(itemId) < MaterialQuota(itemId);

    public int MaterialQuota(string itemId) => Demand(materialQuota, itemId);

    public int MaterialStock(string itemId) => Demand(materialStock, itemId);

    public bool CompletesUsefulRecipe(string itemId) => usefulRecipes.Any(recipe =>
        recipe.Contains(itemId) && recipe.Where(component => component != itemId)
            .All(component => MaterialStock(component) > 0));

    public int MaterialDemandBreadth(string itemId) =>
        usefulRecipes.Count(recipe => recipe.Contains(itemId));

    public bool NeedsProtection(ItemType type) => requiredHazards.Any(hazard =>
        type.GetProtection(hazard) != null);

    public bool ProvidesMissionProtection(ItemType type) =>
        MissionHazard != null && type.GetProtection(MissionHazard) != null;

    public bool IsPolicyAttackWeapon(ItemType type) =>
        IsMeleeWeapon(type) && (type.ID != "item_pitchfork" || pitchforkLimit > 0);

    public bool CanAcquireAttackWeapon(ItemType type) =>
        IsPolicyAttackWeapon(type) &&
        (type.ID != "item_pitchfork" || pitchforkStock < pitchforkLimit);

    public int GlobalItemCount(ItemType type) => Demand(globalItemStock, type.ID);

    public bool CanBuy(ItemType type, int pendingCount = 0) =>
        type.FoodValue > 0 ||
        AiItemPool.IsWaterContainer(type) &&
            pendingCount < CampWaterContainerShortfall ||
        !AiItemPool.IsFirearm(type) &&
        GlobalItemCount(type) + pendingCount < MaximumPurchasedItemsPerType;

    public bool IsStrategic(ItemType type)
    {
        if (PlannedSettlementPaymentType == type ||
            type.HealValue > 0 && DoctorPaymentNeeded ||
            AiItemPool.IsWaterContainer(type) &&
                (ImmediateWaterNeeded || AttackWaterNeeded ||
                    PortableWaterCapacity < DesiredPortableWaterCapacity ||
                    PortableWaterCapacity < DesiredAttackWaterContainerCapacity ||
                    CampWaterContainerShortfall > 0) ||
            type.FoodValue > 0 && PortableFood < DesiredPortableFood ||
            NeedsProduction(type) || NeedsMaterial(type.ID) ||
            Trading.IsPump(type) && PumpStock < PumpQuota ||
            CanAcquireAttackWeapon(type) && MeleeWeaponStock < MeleeWeaponQuota ||
            AiItemPool.IsHazardProtection(type) &&
                (NeedsProtection(type) || ProtectionStock < ProtectionQuota))
            return true;
        return false;
    }

    static bool IsMeleeWeapon(Item item) => IsMeleeWeapon(item.Type);

    static bool IsMeleeWeapon(ItemType type) =>
        type.DamageValue > 0 && !AiItemPool.IsFirearm(type);

    void AddOpportunity(IEnumerable<IEnumerable<string>> recipes)
    {
        HashSet<string>[] exactRecipes = recipes
            .Select(recipe => recipe.ToHashSet())
            .ToArray();
        usefulRecipes.AddRange(exactRecipes);
        foreach (string material in exactRecipes.SelectMany(recipe => recipe).Distinct())
            Add(materialQuota, material);
    }

    static int Demand(Dictionary<string, int> values, string id) =>
        values.TryGetValue(id, out int value) ? value : 0;

    static void Add(Dictionary<string, int> values, string id, int count = 1) =>
        values[id] = Demand(values, id) + count;
}
