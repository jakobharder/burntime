using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class TradeTask
{
    internal static IEnumerable<ConstructionOpportunity> UsefulConstructionOpportunities(ClassicAiState state)
    {
        int meatDemand = MissingProductionToolCount(state, "item_meat");
        int ratDemand = MissingProductionToolCount(state, "item_rats");
        int snakeDemand = MissingProductionToolCount(state, "item_snake");
        bool satisfyOwnedCampsFirst = meatDemand + ratDemand + snakeDemand > 0;

        if (meatDemand > 0 || !satisfyOwnedCampsFirst &&
            HasPotentialProductionNeed(state, "item_meat"))
            yield return new("item_trap", new[] { "item_spring", "item_tin", "item_wire" },
                90 + meatDemand * 200);

        if (ratDemand > 0 || !satisfyOwnedCampsFirst &&
            HasPotentialProductionNeed(state, "item_rats"))
            yield return new("item_rat_trap", new[] { "item_wire", "item_woodpile", "item_screws" },
                75 + ratDemand * 200);

        if (NeedsAnyPump(state) &&
            (!ExpansionTask.ShouldPrioritizeEconomicGrowth(state) || NeedsCriticalPump(state)))
        {
            yield return new("item_hand_pump", new[] { "item_broken_pump", "item_rags", "item_hose" }, 45);
            yield return new("item_industrial_pump",
                new[] { "item_spare_parts", "item_iron_pipe", "item_rags", "item_hose" }, 55);
        }

        ItemType protectiveSuit = state.RootGame.ItemTypes["item_protection_suit"];
        if (NeedsDangerProtection(state, protectiveSuit))
            yield return new("item_protective_suit",
                new[] { "item_gas_mask", "item_gloves", "item_protective_overall", "item_boots" }, 50);
    }

    internal static bool HasConstructionComponent(ClassicAiState state, string itemId) =>
        state.Pool.GetConstructionMaterialCount(itemId) > 0 ||
        state.Player.Group.SelectMany(character => character.Items).Any(item => item.ID == itemId);

    internal static bool HasCompleteUsefulRecipe(ClassicAiState state) => UsefulConstructionOpportunities(state)
        .Any(opportunity => opportunity.Materials.All(component => HasConstructionComponent(state, component)));

    internal static bool CanUseCompleteRecipeAtCamp(ClassicAiState state, Location camp) =>
        UsefulConstructionOpportunities(state)
            .Where(opportunity => opportunity.Materials.All(component => HasConstructionComponent(state, component)))
            .Any(opportunity => opportunity.Result switch
            {
                "item_trap" => NeedsProductionResult(state, camp, "item_meat"),
                "item_rat_trap" => NeedsProductionResult(state, camp, "item_rats"),
                "item_hand_pump" or "item_industrial_pump" => NeedsPump(camp),
                "item_protective_suit" => camp.Danger != null,
                _ => false
            });

    internal static bool NeedsAnyPump(ClassicAiState state) => state.RootGame.World.Locations
        .Any(location => location.Player == state.Player && NeedsPump(location));

    internal static bool NeedsCriticalPump(ClassicAiState state) => state.RootGame.World.Locations
        .Any(location => location.Player == state.Player && location.GetSourceRoom() != null &&
            !location.GetSourceRoom().Items.Any(IsPump) && location.Source.Water <= 0);

    internal static bool NeedsPump(Location camp)
    {
        Room source = camp.GetSourceRoom();
        if (source == null || source.Items.Any(IsPump))
            return false;
        int guards = camp.CampNPC.Count();
        int strategicMinimum = CampEconomy.HasAdvancedFoodPotential(camp)
            ? CampEconomy.PlentyOfWater
            : 2;
        return camp.Source.Water < System.Math.Max(strategicMinimum, guards + 1);
    }

    internal static bool IsPump(Item item) => IsPump(item.Type);

    internal static bool IsPump(ItemType type) => type.IsClass("pump") || type.ID == "item_industrial_pump";

    internal static bool NeedsProductionResult(
        ClassicAiState state,
        Location camp,
        string productId)
    {
        Production production = camp.ValidProductions
            .FirstOrDefault(candidate => candidate.Produce.ID == productId);
        if (production == null || CampEconomy.ProductionToolCount(camp, production) >=
            CampEconomy.DesiredProductionToolCount(state, camp, production))
            return false;

        float candidateValue = ProductionTradePriority(production);
        float installedValue = camp.ValidProductions
            .Where(candidate => CampEconomy.ProductionToolCount(camp, candidate) > 0)
            .Select(ProductionTradePriority)
            .DefaultIfEmpty(float.MinValue)
            .Max();
        return candidateValue >= installedValue;
    }

    internal static bool HasPotentialProductionNeed(ClassicAiState state, string productId) =>
        HasOwnedProductionNeed(state, productId) ||
        state.RootGame.World.Locations.Any(location => !location.IsCity && location.Player == null &&
            location.ValidProductions.Any(production => production.Produce.ID == productId));

    internal static bool HasOwnedProductionNeed(ClassicAiState state, string productId) =>
        state.RootGame.World.Locations.Any(location =>
            location.Player == state.Player && location.Danger == null &&
            NeedsProductionResult(state, location, productId));

    internal static void CollectRedundantProductionTools(ClassicAiState state, Location camp)
    {
        List<Item> collected = new();
        HashSet<Item> reservedWeapons = LocalOpportunities.CampStoredWeaponReserveItems(camp);
        float bestInstalledValue = camp.ValidProductions
            .Where(production => CampEconomy.ProductionToolCount(camp, production) > 0)
            .Select(ProductionTradePriority)
            .DefaultIfEmpty(float.MinValue)
            .Max();
        foreach (Production production in camp.ValidProductions)
        {
            int desired = ProductionTradePriority(production) < bestInstalledValue
                ? 0
                : CampEconomy.DesiredProductionToolCount(state, camp, production);
            int excess = CampEconomy.ProductionToolCount(camp, production) - desired;
            if (excess <= 0)
                continue;

            IEnumerable<(IItemCollection Owner, Item Item)> candidates = camp.Rooms
                .SelectMany(room => room.Items
                    .Where(item => item.Type.Production == production &&
                        !reservedWeapons.Contains(item))
                    .Select(item => ((IItemCollection)room.Items, item)))
                .Concat(camp.CampNPC
                    .Where(npc => npc.Player == state.Player)
                    .SelectMany(npc => npc.Items
                        .Where(item => item.Type.Production == production && npc.Weapon != item)
                        .Select(item => ((IItemCollection)npc.Items, item))));

            foreach ((IItemCollection owner, Item item) in candidates.Take(excess).ToArray())
            {
                owner.Remove(item);
                state.Pool.Insert(item);
                collected.Add(item);
            }
        }

        if (collected.Count > 0)
            AiTelemetry.Report(state.Player,
                $"reclaimed redundant production tools from {camp.Title}: " +
                string.Join(", ", collected.GroupBy(item => item.ID)
                    .Select(group => group.Count() > 1 ? $"{group.Key} x{group.Count()}" : group.Key)));
    }

    internal static bool HasNeutralExpansionOpportunity(ClassicAiState state) => state.RootGame.World.Locations
        .Any(location => !location.IsCity && location.Player == null);

    internal static bool IsUsefulForNeutralExpansion(ClassicAiState state, Production production) =>
        state.RootGame.World.Locations.Any(location => !location.IsCity && location.Player == null &&
            location.ValidProductions.Contains(production));

    internal static bool HasWaterContainer(Character character) => character.Items
        .Any(item => AiItemPool.IsWaterContainer(item.Type));

    internal static IEnumerable<IItemCollection> GetLocalItemSources(ClassicAiState state, Location camp)
    {
        foreach (Character character in state.Player.Group)
            yield return character.Items;
        foreach (Character character in camp.CampNPC.Where(npc => npc.Player == state.Player))
            yield return character.Items;
        foreach (Room room in camp.Rooms)
            yield return room.Items;
    }

    internal sealed record ConstructionOpportunity(string Result, string[] Materials, int EconomicValue);
}
