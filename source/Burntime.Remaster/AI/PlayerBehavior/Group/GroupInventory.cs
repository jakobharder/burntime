using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class GroupInventory
{
    internal static Character? FindCargoCarrier(ClassicAiState state, Item item)
    {
        Character leader = state.Player.Character;
        if (SatisfiesMissingLeaderRole(leader, item) && CanCarryCargo(state, leader, item))
            return leader;

        return state.Player.Group.FirstOrDefault(character => CanCarryCargo(state, character, item));
    }

    internal static bool CanCarryCargo(
        ClassicAiState state,
        Character character,
        Item item)
        => CanCarryCargo(state, character, item.Type);

    internal static bool CanCarryCargo(
        ClassicAiState state,
        Character character,
        ItemType type)
    {
        if (character.Items.IsFull)
            return false;
        if (character != state.Player.Character)
            return true;

        int freeAfter = character.Items.MaxCount - character.Items.Count - 1;
        return freeAfter >= MissingLeaderRoleSlots(character, type);
    }

    internal static bool CanReplaceCargo(
        ClassicAiState state,
        Character character,
        Item removed,
        Item replacement)
    {
        if (character != state.Player.Character)
            return true;

        int missingBefore = MissingLeaderRoleSlots(character);
        int missingAfter = MissingLeaderRoleSlots(character, replacement, removed);
        int freeAfter = character.Items.MaxCount - character.Items.Count;
        return missingAfter < missingBefore || freeAfter >= missingAfter;
    }

    internal static int MissingLeaderRoleSlotsAfter(
        ClassicAiState state,
        IEnumerable<Item> removed,
        IEnumerable<Item> added)
    {
        HashSet<Item> removedSet = removed.ToHashSet();
        Item[] resulting = state.Player.Character.Items
            .Where(item => !removedSet.Contains(item))
            .Concat(added)
            .ToArray();
        return MissingRoleSlots(resulting);
    }

    /// <summary>
    /// Keeps the leader's food, water-container and hazard-protection roles from
    /// being consumed by ordinary cargo. Surplus cargo is moved to followers
    /// first and only deposited when the group is standing at an owned camp.
    /// </summary>
    internal static void MaintainLeaderRoleSlots(ClassicAiState state)
    {
        Character leader = state.Player.Character;
        while (leader.Items.MaxCount - leader.Items.Count < MissingLeaderRoleSlots(leader))
        {
            Item? displaced = leader.Items
                .Where(item => !IsFoodRole(item) && !IsWaterRole(item) && !IsProtectionRole(item))
                .Where(item => item != leader.Weapon && item != leader.Protection)
                .Where(item => Recruitment.PlannedFutureSettlementPaymentType(state) != item.Type)
                .OrderBy(item => CargoManagement.CargoRetentionValue(state, item))
                .ThenBy(item => item.TradeValue)
                .FirstOrDefault();
            if (displaced == null)
                return;

            Character? follower = state.Player.Group
                .Where(character => character != leader && !character.Items.IsFull)
                .OrderBy(character => character.Items.Count)
                .FirstOrDefault();
            leader.Items.Remove(displaced);
            if (follower != null && follower.Items.Add(displaced))
            {
                AiTelemetry.Report(state.Player,
                    $"moved {displaced.ID} from {leader.Name} to {follower.Name} to preserve leader survival slots");
                continue;
            }

            if (state.Current.Player == state.Player && CampManagement.StoreItemInCamp(state.Current, displaced))
            {
                AiTelemetry.Report(state.Player,
                    $"stored {displaced.ID} at {state.Current.Title} to preserve leader survival slots");
                continue;
            }

            leader.Items.Add(displaced);
            return;
        }

        // Food is deliberately first: camp production is capped, so carrying a
        // physical food item is preferable to leaving its reserved slot empty.
        FillMissingLeaderRole(state, IsFoodRole, item => item.FoodValue);
        FillMissingLeaderRole(state, IsWaterRole,
            item => AiItemPool.WaterContainerCapacity(item.Type));
        FillMissingLeaderRole(state, IsProtectionRole, item => item.DefenseValue);
    }

    static void FillMissingLeaderRole(
        ClassicAiState state,
        Func<Item, bool> role,
        Func<Item, int> roleValue)
    {
        Character leader = state.Player.Character;
        if (leader.Items.Any(role) || leader.Items.IsFull)
            return;

        Item? item = state.Player.Group
            .Where(character => character != leader)
            .SelectMany(character => character.Items)
            .Where(role)
            .OrderByDescending(roleValue)
            .ThenByDescending(item => item.TradeValue)
            .FirstOrDefault();
        Character? owner = item == null ? null : state.Player.Group
            .First(character => character.Items.Contains(item));
        Room? room = null;
        if (item == null && state.Current.Player == state.Player)
        {
            var stored = state.Current.Rooms
                .SelectMany(candidate => candidate.Items
                    .Where(role)
                    .Select(candidateItem => (Room: candidate, Item: candidateItem)))
                .OrderByDescending(entry => roleValue(entry.Item))
                .ThenByDescending(entry => entry.Item.TradeValue)
                .FirstOrDefault();
            room = stored.Room;
            item = stored.Item;
        }
        if (item == null)
            return;

        owner?.Items.Remove(item);
        room?.Items.Remove(item);
        if (leader.Items.Add(item))
            return;
        owner?.Items.Add(item);
        room?.Items.Add(item);
    }

    internal static bool SatisfiesMissingLeaderRole(Character leader, Item item) =>
        IsFoodRole(item) && !leader.Items.Any(IsFoodRole) ||
        IsWaterRole(item) && !leader.Items.Any(IsWaterRole) ||
        IsProtectionRole(item) && !leader.Items.Any(IsProtectionRole);

    static int MissingLeaderRoleSlots(
        Character leader,
        Item? added = null,
        Item? removed = null) => MissingRoleSlots(leader.Items
            .Where(item => item != removed)
            .Concat(added == null ? Array.Empty<Item>() : new[] { added }));

    static int MissingLeaderRoleSlots(Character leader, ItemType added) =>
        (leader.Items.Any(IsFoodRole) || added.FoodValue > 0 ? 0 : 1) +
        (leader.Items.Any(IsWaterRole) || AiItemPool.IsWaterContainer(added) ? 0 : 1) +
        (leader.Items.Any(IsProtectionRole) || AiItemPool.IsHazardProtection(added) ? 0 : 1);

    static int MissingRoleSlots(IEnumerable<Item> items)
    {
        Item[] cargo = items.ToArray();
        return (cargo.Any(IsFoodRole) ? 0 : 1) +
            (cargo.Any(IsWaterRole) ? 0 : 1) +
            (cargo.Any(IsProtectionRole) ? 0 : 1);
    }

    static bool IsFoodRole(Item item) => item.FoodValue > 0;
    static bool IsWaterRole(Item item) => AiItemPool.IsWaterContainer(item.Type);
    static bool IsProtectionRole(Item item) => AiItemPool.IsHazardProtection(item.Type);

    internal static bool ConsumeAvailableSupplies(ClassicAiState state)
    {
        Player player = state.Player;
        Location current = state.Current;
        bool consumed = false;
        ItemType? reservedPayment = Recruitment.PlannedFutureSettlementPaymentType(state);
        while (player.Character.Food <= 5 || player.Group.Any(character =>
            character != player.Character && character.Food <= 3))
        {
            List<(IItemCollection Owner, Item Food)> candidates = player.Group
                .SelectMany(character => character.Items
                    .Where(item => item.FoodValue > 0 &&
                        reservedPayment != item.Type)
                    .Select(item => ((IItemCollection)character.Items, item)))
                .ToList();
            if (current.Player == player)
            {
                int storedFood = current.Rooms.Sum(room =>
                    room.Items.Count(item => item.FoodValue > 0));
                int campReserve = CampManagement.CampFoodItemReserve;
                if (storedFood > campReserve)
                    candidates.AddRange(current.Rooms.SelectMany(room => room.Items
                        .Where(item => item.FoodValue > 0 &&
                            reservedPayment != item.Type)
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
            Item water = player.Group.SelectMany(character => character.Items)
                .Where(item => item.WaterValue > 0 && item.Type != reservedPayment)
                .OrderByDescending(item => item.WaterValue)
                .FirstOrDefault();
            if (water == null)
                break;
            player.Group.Drink(null, water.WaterValue);
            water.Type = water.Type.Empty;
            consumed = true;
        }
        return consumed;
    }

    internal static void RemoveAdviceItems(ClassicAiState state)
    {
        IEnumerable<IItemCollection> inventories = state.Player.Group
            .Select(character => (IItemCollection)character.Items)
            .Concat(state.RootGame.World.Locations
                .Where(location => location.Player == state.Player)
                .SelectMany(location => location.Rooms.Select(room => (IItemCollection)room.Items)
                    .Concat(location.CampNPC
                        .Where(character => character.Player == state.Player)
                        .Select(character => (IItemCollection)character.Items))));
        foreach (IItemCollection inventory in inventories)
        {
            foreach (Item advice in inventory.Where(item => item.ID == "item_advice").ToArray())
                inventory.Remove(advice);
        }
    }

}
