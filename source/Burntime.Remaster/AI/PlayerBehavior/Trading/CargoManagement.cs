using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class CargoManagement
{
    internal static void FillCityCaravan(ClassicAiState state, Location camp)
    {
        if (state.Player.Group.GetFreeSlotCount() == 0)
            return;

        HashSet<Item> reservedWeapons = CampManagement.CampStoredWeaponReserveItems(camp);
        HashSet<Item> reservedWater = CampManagement.CampStoredWaterReserveItems(camp);
        var candidates = camp.Rooms
            .SelectMany(room => room.Items.Select(item => new { Room = room, Item = item }))
            .Where(entry => entry.Item.FoodValue == 0 && entry.Item.TradeValue > 0)
            .Where(entry => !reservedWeapons.Contains(entry.Item) &&
                !reservedWater.Contains(entry.Item) &&
                !AiItemPool.IsWaterContainer(entry.Item.Type) &&
                !Trading.IsPump(entry.Item))
            .Where(entry => entry.Item.Type.Production == null ||
                !camp.ValidProductions.Contains(entry.Item.Type.Production))
            .OrderByDescending(entry => entry.Item.TradeValue)
            .ToArray();

        List<Item> loaded = new();
        foreach (var candidate in candidates)
        {
            Character carrier = GroupInventory.FindCargoCarrier(state, candidate.Item);
            if (carrier == null)
                break;
            candidate.Room.Items.Remove(candidate.Item);
            carrier.Items.Add(candidate.Item);
            loaded.Add(candidate.Item);
        }

        if (loaded.Count > 0)
            AiTelemetry.Report(state.Player,
                $"filled city caravan from {camp.Title} with high-value reserve cargo: " +
                string.Join(", ", loaded.GroupBy(item => item.ID)
                    .Select(group => group.Count() > 1 ? $"{group.Key} x{group.Count()}" : group.Key)));
    }

    internal static float CampCollectibleValue(ClassicAiState state, Location camp)
        => ProjectedCampCollectibleValue(state, camp, 0);

    internal static float ProjectedCampCollectibleValue(ClassicAiState state, Location camp, int travelDays)
    {
        float value = 0;
        if (camp.Production != null)
        {
            int activeStock = camp.Rooms.Sum(room =>
                room.Items.GetCount(camp.Production.Produce));
            int freeProductionStock = System.Math.Max(0,
                Location.MaxStockFood - camp.GetCurrentProductionStockCount());
            int stock = activeStock + System.Math.Min(freeProductionStock,
                ProjectedProductionItems(camp, travelDays));
            int reserve = CampManagement.CampFoodItemReserve;
            value += System.Math.Max(0, stock - reserve) * camp.Production.Produce.TradeValue;
        }

        value += camp.Rooms.SelectMany(room => room.Items)
            .Where(item => (camp.Production == null || item.Type != camp.Production.Produce) &&
                (item.Type.Production == null || !camp.ValidProductions.Contains(item.Type.Production)) &&
                !Trading.IsPump(item) && CanCollectForTrade(state, item))
            .Sum(item => item.TradeValue);
        return value;
    }

    internal static int ProjectedCampCollectibleCount(ClassicAiState state, Location camp, int travelDays)
    {
        int count = 0;
        if (camp.Production != null)
        {
            int activeStock = camp.Rooms.Sum(room =>
                room.Items.GetCount(camp.Production.Produce));
            int freeProductionStock = System.Math.Max(0,
                Location.MaxStockFood - camp.GetCurrentProductionStockCount());
            int stock = activeStock + System.Math.Min(freeProductionStock,
                ProjectedProductionItems(camp, travelDays));
            int reserve = CampManagement.CampFoodItemReserve;
            count += System.Math.Max(0, stock - reserve);
        }

        count += camp.Rooms.SelectMany(room => room.Items)
            .Count(item => (camp.Production == null || item.Type != camp.Production.Produce) &&
                (item.Type.Production == null || !camp.ValidProductions.Contains(item.Type.Production)) &&
                !Trading.IsPump(item) && CanCollectForTrade(state, item));
        return count;
    }

    internal static int ProjectedProductionItems(Location camp, int travelDays)
    {
        if (camp.Production == null || travelDays <= 0)
            return 0;
        Production.Rate rate = camp.GetFoodProductionRate();
        if (rate.ItemDropInterval <= 0)
            return 0;
        return (int)(travelDays / rate.ItemDropInterval);
    }

    internal static bool CanCollectForTrade(ClassicAiState state, Item item)
    {
        if (!AiItemPool.IsWaterContainer(item.Type))
            return Trading.CanSell(state, item);

        int availableContainers = state.Player.Group.SelectMany(character => character.Items)
            .Count(candidate => AiItemPool.IsWaterContainer(candidate.Type)) +
            state.Reserve.WaterContainerCount;
        return availableContainers >= state.Player.Group.Count;
    }

    public static bool TryReplaceCargo(
        ClassicAiState state,
        Item found,
        out Item replaced,
        out Character carrier)
    {
        replaced = null;
        carrier = null;
        int food = Trading.PortableFoodSupply(state);
        int waterCapacity = Trading.PortableWaterSupply(state);

        var candidate = state.Player.Group
            .SelectMany(character => character.Items.Select(item => new { Character = character, Item = item }))
            .Where(entry => Trading.CanSell(state, entry.Item))
            .Where(entry => GroupInventory.CanReplaceCargo(state, entry.Character, entry.Item, found))
            .Where(entry => food - entry.Item.FoodValue + found.FoodValue >=
                Math.Min(food, Trading.DesiredPortableFood(state)))
            .Where(entry => waterCapacity - AiItemPool.WaterContainerCapacity(entry.Item.Type) +
                AiItemPool.WaterContainerCapacity(found.Type) >=
                Math.Min(waterCapacity, Trading.DesiredPortableWaterCapacity(state)))
            .OrderByDescending(entry => entry.Character == state.Player.Character &&
                GroupInventory.SatisfiesMissingLeaderRole(state.Player.Character, found))
            .ThenBy(entry => CargoRetentionValue(state, entry.Item))
            .ThenBy(entry => entry.Item.TradeValue)
            .FirstOrDefault();
        if (candidate == null || CargoRetentionValue(state, found) <= CargoRetentionValue(state, candidate.Item))
            return false;

        candidate.Character.Items.Remove(candidate.Item);
        replaced = candidate.Item;
        carrier = candidate.Character;
        return true;
    }

    internal static float CargoRetentionValue(ClassicAiState state, Item item)
    {
        float material = Trading.ConstructionMaterialPriority(state, item.ID);
        if (material > 0)
            return 10000 + material;
        if (item.Type.Production != null && Trading.NeedsProduction(state, item.Type))
            return 9000 + Trading.ProductionTradePriority(item.Type.Production);
        if (AiItemPool.IsHazardProtection(item.Type) && Trading.NeedsDangerProtection(state, item.Type))
            return 8000 + item.TradeValue;
        // Food value is also slot efficiency. Once cheap maggots and rats have
        // been eaten, prefer compact meat and snakes over similarly valued cargo
        // without making food untouchable economic capital.
        if (item.FoodValue > 0)
            return item.TradeValue + item.FoodValue * 2;
        return item.TradeValue;
    }

}
