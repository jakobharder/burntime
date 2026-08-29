using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class GroupManagement
{
    /// <summary>
    /// Handles only the travelling party. Use this after recruitment or barter;
    /// full camp redistribution is intentionally limited to once per AI turn.
    /// </summary>
    internal static void MaintainGroupEquipment(
        ClassicAiState state,
        bool allowCampTransfers = false)
    {
        Player player = state.Player;
        Character[] group = player.Group.Where(character => !character.IsDead).ToArray();
        WeaponLoadout.NormalizeWeaponLimits(state, group);
        NormalizeCarriedProtection(state, group);

        // Keep one weapon on every traveller before spending weapons on camp upgrades.
        foreach (Character traveller in group)
            WeaponLoadout.EquipWeapon(state, traveller, group,
                upgradeWeakWeapon: false,
                traveller == player.Character ? "leader" : "follower");
        if (allowCampTransfers)
            TransferRearCampWeapons(state, group, group);
        if (state.HasAttackPlan)
        {
            foreach (Character traveller in group)
                WeaponLoadout.EquipWeapon(state, traveller, group,
                    upgradeWeakWeapon: true,
                    traveller == player.Character ? "leader" : "follower");
        }

        CarryStandingWaterContainers(state, group);
        CarryStrategicProtection(state);
    }

    internal static void CarryStandingWaterContainers(ClassicAiState state, IReadOnlyCollection<Character> group)
    {
        while (Trading.PortableWaterCapacity(state) < Trading.DesiredWaterContainerCapacity(state))
        {
            Character carrier = group
                .Where(character => !character.Items.IsFull)
                .OrderBy(character => character.Items
                    .Where(item => AiItemPool.IsWaterContainer(item.Type))
                    .Sum(item => AiItemPool.WaterContainerCapacity(item.Type)))
                .FirstOrDefault();
            if (carrier == null)
                break;
            Item container = state.Reserve.HasWaterContainer()
                ? state.Reserve.GetBestWaterContainer()
                : state.Current.Player == state.Player
                    ? TakeStoredWaterContainer(state.Current)
                    : null;
            if (container == null)
                break;
            carrier.Items.Add(container);
            AiTelemetry.Report(state.Player,
                $"added standing water reserve {container.ID} to {carrier.Name}");
        }
    }

    static Item TakeStoredWaterContainer(Location camp)
    {
        var stored = camp.Rooms
            .SelectMany(room => room.Items
                .Where(item => AiItemPool.IsWaterContainer(item.Type))
                .Select(item => new { Room = room, Item = item }))
            .OrderByDescending(entry => AiItemPool.WaterContainerCapacity(entry.Item.Type))
            .ThenByDescending(entry => entry.Item.WaterValue)
            .FirstOrDefault();
        if (stored == null)
            return null;
        stored.Room.Items.Remove(stored.Item);
        return stored.Item;
    }

    internal static void ProvisionGroupFromCampSurplus(
        ClassicAiState state,
        Location camp)
    {
        int consumedItems = 0;
        int consumedFood = 0;
        ItemType? reservedPayment = Recruitment.PlannedFutureSettlementPaymentType(state);
        while (state.Player.Group.Any(character => character.Food < character.MaxFood))
        {
            int storedFood = camp.Rooms.Sum(room =>
                room.Items.Count(item => item.FoodValue > 0));
            List<(IItemCollection Owner, Item Item)> candidates = state.Player.Group
                .SelectMany(character => character.Items
                    .Where(item => item.FoodValue > 0 && reservedPayment != item.Type)
                    .Select(item => ((IItemCollection)character.Items, item)))
                .ToList();
            if (storedFood > CampManagement.CampFoodItemReserve)
            {
                candidates.AddRange(camp.Rooms.SelectMany(room => room.Items
                    .Where(item => item.FoodValue > 0 && reservedPayment != item.Type)
                    .Select(item => ((IItemCollection)room.Items, item))));
            }
            (IItemCollection Owner, Item Item) candidate = candidates
                .OrderBy(entry => entry.Item.FoodValue)
                .ThenBy(entry => entry.Item.TradeValue)
                .FirstOrDefault();
            if (candidate.Item == null)
                break;

            int capacity = state.Player.Group.Sum(character =>
                character.MaxFood - character.Food);
            if (candidate.Item.FoodValue > capacity)
                break;

            state.Player.Group.Eat(null, candidate.Item.FoodValue);
            candidate.Owner.Remove(candidate.Item);
            consumedItems++;
            consumedFood += candidate.Item.FoodValue;
        }

        if (consumedItems > 0)
            AiTelemetry.Report(state.Player,
                $"provisioned group at {camp.Title} before collecting exports " +
                $"({consumedItems} food items, {consumedFood} food value)");
    }

    internal static void PrepareCampWaterReservesForDeparture(ClassicAiState state)
    {
        if (state.Current.Player != state.Player ||
            ExchangeCampWaterReserves(state, state.Current) == 0)
            return;

        if (GroupInventory.ConsumeAvailableSupplies(state))
            AiTelemetry.Report(state.Player,
                "consumed camp water reserves before departure");
    }

    static int ExchangeCampWaterReserves(ClassicAiState state, Location camp)
    {
        var carriedEmpties = state.Player.Group
            .SelectMany(character => character.Items
                .Where(item => item.WaterValue == 0 &&
                    AiItemPool.IsWaterContainer(item.Type))
                .Select(item => new { Character = character, Item = item }))
            .OrderByDescending(entry =>
                AiItemPool.WaterContainerCapacity(entry.Item.Type))
            .ToList();
        if (carriedEmpties.Count == 0)
            return 0;

        var storedWater = camp.Rooms
            .SelectMany(room => room.Items
                .Where(item => item.WaterValue > 0 &&
                    AiItemPool.IsWaterContainer(item.Type))
                .Select(item => new
                {
                    Owner = (IItemCollection)room.Items,
                    Item = item
                }))
            .OrderByDescending(entry => entry.Item.WaterValue)
            .ToList();
        int exchanges = System.Math.Min(carriedEmpties.Count, storedWater.Count);
        if (exchanges == 0)
            return 0;

        List<string> withdrawn = new();
        for (int index = 0; index < exchanges; index++)
        {
            var carried = carriedEmpties[index];
            var stored = storedWater[index];
            carried.Character.Items.Remove(carried.Item);
            stored.Owner.Remove(stored.Item);
            carried.Character.Items.Add(stored.Item);
            stored.Owner.Add(carried.Item);
            withdrawn.Add(stored.Item.ID);
        }

        AiTelemetry.Report(state.Player,
            $"exchanged empty containers for camp water reserves at {camp.Title}: " +
            string.Join(", ", withdrawn));
        return exchanges;
    }

    internal static void TransferRearCampWeapons(
        ClassicAiState state,
        IEnumerable<Character> travellers,
        IReadOnlyCollection<Character> group)
    {
        Location camp = state.Current;
        Character[] unarmedTravellers = travellers.Where(character =>
            (character.Items.FindBestWeapon()?.DamageValue ?? 0) == 0).ToArray();
        bool groupIsCompletelyUnarmed = unarmedTravellers.Length > 0 && group.All(character =>
            (character.Items.FindBestWeapon()?.DamageValue ?? 0) == 0);
        bool threatened = ReinforcementPlanning.IsThreatened(state, camp) ||
            state.WasRecentlyContested(camp);
        bool emergencyBorrow = threatened && groupIsCompletelyUnarmed &&
            EconomicSupport.HasBeenStrategicallyStalled(state, turns: 12);
        if (camp.Player != state.Player || threatened && !emergencyBorrow)
            return;

        foreach (Character traveller in unarmedTravellers)
        {
            var stored = camp.Rooms
                .SelectMany(room => room.Items.Select(item => new { Room = room, Item = item }))
                .Where(entry => WeaponLoadout.IsMeleeWeapon(entry.Item) &&
                    CanWithdrawRearWeapon(state, camp, entry.Item) &&
                    WeaponLoadout.WeaponAllowed(state, group, traveller, entry.Item.Type))
                .OrderBy(entry => entry.Item.DamageValue)
                .FirstOrDefault();
            if (stored != null)
            {
                stored.Room.Items.Remove(stored.Item);
                if (!state.Reserve.Insert(stored.Item))
                {
                    stored.Room.Items.Add(stored.Item);
                    continue;
                }
                AiTelemetry.Report(state.Player,
                    $"withdrew reserve {stored.Item.ID} from {camp.Title} for the travelling group");
                WeaponLoadout.EquipWeapon(state, traveller, group,
                    upgradeWeakWeapon: false,
                    traveller == state.Player.Character ? "leader" : "follower");
                continue;
            }

            Character guard = camp.CampNPC
                .Where(character => character.Player == state.Player && !character.IsDead)
                .Where(character => character.Items.FindBestWeapon() is Item weapon &&
                    !AiItemPool.IsFirearm(weapon.Type) &&
                    CanWithdrawRearWeapon(state, camp, weapon) &&
                    WeaponLoadout.WeaponAllowed(state, group, traveller, weapon.Type))
                .OrderBy(character => character.Items.FindBestWeapon()!.DamageValue)
                .FirstOrDefault();
            if (guard == null)
                return;

            Item transferred = guard.Items.FindBestWeapon();
            if (guard.Weapon == transferred)
                guard.Weapon = null;
            guard.Items.Remove(transferred);
            if (!state.Reserve.Insert(transferred))
            {
                guard.Items.Add(transferred);
                guard.Weapon = transferred;
                continue;
            }
            AiTelemetry.Report(state.Player,
                threatened
                    ? $"borrowed {transferred.ID} from guard {guard.Name} at threatened {camp.Title} " +
                        "to unblock the travelling group"
                    : $"transferred {transferred.ID} from rear guard {guard.Name} at {camp.Title} " +
                        "to the travelling group");
            WeaponLoadout.EquipWeapon(state, traveller, group,
                upgradeWeakWeapon: false,
                traveller == state.Player.Character ? "leader" : "follower");
        }
    }

    static bool CanWithdrawRearWeapon(ClassicAiState state, Location camp, Item weapon)
    {
        Production? production = weapon.Type.Production;
        if (production == null || camp.Production != production ||
            CampEconomy.ProductionToolCount(camp, production) > 1)
            return true;

        // A knife or similar weapon can be the camp's only production tool.
        // Keep it on the guard unless base output or a stocked neighboring camp
        // can support the travellers without it.
        return ExpansionPlanning.CanBootstrapCamp(state, camp);
    }

    internal static void CarryStrategicProtection(ClassicAiState state)
    {
        int desired = System.Math.Min(state.Player.Group.Count, Trading.DesiredProtectionReserve(state));
        while (state.Player.Group.SelectMany(character => character.Items)
                   .Count(item => AiItemPool.IsHazardProtection(item.Type)) < desired &&
               state.Reserve.ProtectionCount > 0)
        {
            Character carrier = state.Player.Group.FirstOrDefault(character =>
                !character.Items.IsFull && !character.Items.Any(item =>
                    AiItemPool.IsHazardProtection(item.Type)));
            if (carrier == null)
                return;
            Item protection = state.Reserve.GetBestGeneralProtection();
            if (protection == null)
                return;
            carrier.Items.Add(protection);
            carrier.Protection = protection;
            AiTelemetry.Report(state.Player,
                $"carried {protection.ID} on {carrier.Name} as strategic hazard protection");
        }
    }

    internal static void NormalizeCarriedProtection(ClassicAiState state, IEnumerable<Character> group)
    {
        foreach (Character character in group)
        {
            Item[] protection = character.Items
                .Where(item => AiItemPool.IsHazardProtection(item.Type))
                .OrderByDescending(item => item == character.Protection)
                .ThenByDescending(item => item.DefenseValue)
                .ToArray();
            foreach (Item excess in protection.Skip(1))
            {
                character.Items.Remove(excess);
                CampManagement.StoreInReserveOrAtLocation(state, excess, character.Location);
                AiTelemetry.Report(state.Player,
                    $"redistributed excess hazard protection {excess.ID} from {character.Name}");
            }
            if (protection.Length > 0)
                character.Protection = protection[0];
        }
    }

}
