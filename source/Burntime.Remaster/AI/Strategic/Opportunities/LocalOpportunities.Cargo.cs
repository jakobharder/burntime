using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static partial class LocalOpportunities
{
    internal static bool ConsumeAvailableSupplies(ClassicAiState state)
    {
        Player player = state.Player;
        Location current = state.Current;
        bool consumed = false;
        while (player.Group.Any(character => character.Food <= 3))
        {
            List<(IItemCollection Owner, Item Food)> candidates = player.Group
                .SelectMany(character => character.Items
                    .Where(item => item.FoodValue > 0)
                    .Select(item => ((IItemCollection)character.Items, item)))
                .ToList();
            if (current.Player == player)
            {
                int storedFood = current.Rooms.Sum(room =>
                    room.Items.Count(item => item.FoodValue > 0));
                int campReserve = CampFoodItemReserve;
                if (storedFood > campReserve)
                    candidates.AddRange(current.Rooms.SelectMany(room => room.Items
                        .Where(item => item.FoodValue > 0)
                        .Select(item => ((IItemCollection)room.Items, item))));
            }

            (IItemCollection Owner, Item Food) candidate = candidates
                .OrderBy(entry => entry.Food.FoodValue)
                .ThenBy(entry => entry.Food.TradeValue)
                .FirstOrDefault();
            if (candidate.Food == null)
                break;
            int foodCapacity = player.Group.Sum(character => character.MaxFood - character.Food);
            if (candidate.Food.FoodValue > foodCapacity)
                break;
            player.Group.Eat(null, candidate.Food.FoodValue);
            candidate.Owner.Remove(candidate.Food);
            consumed = true;
        }

        while (player.Group.Any(character => character.Water <= 2))
        {
            Item water = player.Group.FindWater();
            if (water == null)
                break;
            player.Group.Drink(null, water.WaterValue);
            water.Type = water.Type.Empty;
            consumed = true;
        }
        return consumed;
    }

    internal static void FillCityCaravan(ClassicAiState state, Location camp)
    {
        if (state.Player.Group.GetFreeSlotCount() == 0)
            return;

        HashSet<Item> reservedWeapons = CampStoredWeaponReserveItems(camp);
        var candidates = camp.Rooms
            .SelectMany(room => room.Items.Select(item => new { Room = room, Item = item }))
            .Where(entry => entry.Item.FoodValue == 0 && entry.Item.TradeValue > 0)
            .Where(entry => !reservedWeapons.Contains(entry.Item) && !TradeTask.IsPump(entry.Item))
            .Where(entry => entry.Item.Type.Production == null ||
                !camp.ValidProductions.Contains(entry.Item.Type.Production))
            .OrderByDescending(entry => entry.Item.TradeValue)
            .ToArray();

        List<Item> loaded = new();
        foreach (var candidate in candidates)
        {
            Character carrier = state.Player.Group.FirstOrDefault(character => !character.Items.IsFull);
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
            int stock = camp.Rooms.Sum(room => room.Items.GetCount(camp.Production.Produce));
            stock = System.Math.Min(Location.MaxStockFood,
                stock + ProjectedProductionItems(camp, travelDays));
            int reserve = CampFoodItemReserve;
            value += System.Math.Max(0, stock - reserve) * camp.Production.Produce.TradeValue;
        }

        value += camp.Rooms.SelectMany(room => room.Items)
            .Where(item => (camp.Production == null || item.Type != camp.Production.Produce) &&
                (item.Type.Production == null || !camp.ValidProductions.Contains(item.Type.Production)) &&
                !TradeTask.IsPump(item) && CanCollectForTrade(state, item))
            .Sum(item => item.TradeValue);
        return value;
    }

    internal static int ProjectedCampCollectibleCount(ClassicAiState state, Location camp, int travelDays)
    {
        int count = 0;
        if (camp.Production != null)
        {
            int stock = camp.Rooms.Sum(room => room.Items.GetCount(camp.Production.Produce));
            stock = System.Math.Min(Location.MaxStockFood,
                stock + ProjectedProductionItems(camp, travelDays));
            int reserve = CampFoodItemReserve;
            count += System.Math.Max(0, stock - reserve);
        }

        count += camp.Rooms.SelectMany(room => room.Items)
            .Count(item => (camp.Production == null || item.Type != camp.Production.Produce) &&
                (item.Type.Production == null || !camp.ValidProductions.Contains(item.Type.Production)) &&
                !TradeTask.IsPump(item) && CanCollectForTrade(state, item));
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
            return TradeTask.CanSell(state, item);

        int availableContainers = state.Player.Group.SelectMany(character => character.Items)
            .Count(candidate => AiItemPool.IsWaterContainer(candidate.Type)) +
            state.Pool.WaterContainerCount;
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
        int food = TradeTask.PortableFoodSupply(state);
        int waterCapacity = TradeTask.PortableWaterSupply(state);

        var candidate = state.Player.Group
            .SelectMany(character => character.Items.Select(item => new { Character = character, Item = item }))
            .Where(entry => TradeTask.CanSell(state, entry.Item))
            .Where(entry => food - entry.Item.FoodValue + found.FoodValue >=
                Math.Min(food, TradeTask.DesiredPortableFood(state)))
            .Where(entry => waterCapacity - AiItemPool.WaterContainerCapacity(entry.Item.Type) +
                AiItemPool.WaterContainerCapacity(found.Type) >=
                Math.Min(waterCapacity, TradeTask.DesiredPortableWaterCapacity(state)))
            .OrderBy(entry => CargoRetentionValue(state, entry.Item))
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
        float material = TradeTask.ConstructionMaterialPriority(state, item.ID);
        if (material > 0)
            return 10000 + material;
        if (item.Type.Production != null && TradeTask.NeedsProduction(state, item.Type))
            return 9000 + TradeTask.ProductionTradePriority(item.Type.Production);
        if (AiItemPool.IsHazardProtection(item.Type) && TradeTask.NeedsDangerProtection(state, item.Type))
            return 8000 + item.TradeValue;
        return item.TradeValue;
    }

}
