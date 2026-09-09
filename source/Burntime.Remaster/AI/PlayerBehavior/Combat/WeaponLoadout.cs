using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

/// <summary>
/// Shared mechanics for assigning weapons. Camp and travelling-group policy
/// remains in their respective maintenance files.
/// </summary>
internal static class WeaponLoadout
{
    internal static bool IsMeleeWeapon(Item item) =>
        item.DamageValue > 0 && !AiItemPool.IsFirearm(item.Type);

    internal static void EquipWeapon(
        ClassicAiState state,
        Character character,
        IReadOnlyCollection<Character> unit,
        bool upgradeWeakWeapon,
        string role,
        int maximumPriority = int.MaxValue)
    {
        Item current = character.Items.FindBestWeapon();
        int currentDamage = current?.DamageValue ?? 0;
        int desiredMinimum = upgradeWeakWeapon
            ? currentDamage
            : currentDamage > 0 ? int.MaxValue : 0;
        bool reserveProductionTool = ExpansionPlanning.ShouldReserveProductionTool(state);
        bool allowProductionTool = currentDamage == 0 || !reserveProductionTool;
        bool Allowed(ItemType type) => type.WeaponPriority <= maximumPriority &&
            WeaponAllowed(state, unit, character, type);
        if (desiredMinimum == int.MaxValue ||
            !state.Reserve.HasBetterWeapon(desiredMinimum, allowProductionTool, Allowed))
        {
            if (current != null)
                character.Weapon = current;
            return;
        }

        Item weapon = state.Reserve.GetBestWeapon(Allowed, desiredMinimum, allowProductionTool);
        if (weapon == null)
            return;

        if (character.Items.IsFull && current != null)
        {
            character.Items.Remove(current);
            CampManagement.StoreInReserveOrAtLocation(state, current, character.Location);
        }
        else if (character.Items.IsFull)
        {
            Item replaceable = character.Items
                .Where(item => Trading.CanSell(state, item))
                .OrderBy(item => CargoManagement.CargoRetentionValue(state, item))
                .ThenBy(item => item.TradeValue)
                .FirstOrDefault();
            if (replaceable == null)
            {
                CampManagement.StoreInReserveOrAtLocation(state, weapon, character.Location);
                return;
            }
            character.Items.Remove(replaceable);
            state.Current.Items.Add(replaceable);
            AiTelemetry.Report(state.Player,
                $"dropped lower-value cargo {replaceable.ID} so {character.Name} can carry a weapon");
        }
        if (!character.Items.Add(weapon))
        {
            CampManagement.StoreInReserveOrAtLocation(state, weapon, character.Location);
            return;
        }

        character.Weapon = weapon;
        string location = character.IsStationed ? $" at {character.Location.Title}" : "";
        AiTelemetry.Report(state.Player,
            $"equipped {role} {character.Name}{location} with {weapon.ID}");
    }

    // Moving a weapon from room storage onto a guard keeps it in the same
    // camp's production count; no tool or weapon is created or removed.
    internal static Item? EquipStoredCampWeapon(Character guard, System.Func<ItemType, bool> allowed)
    {
        Item? current = guard.Items.FindBestWeapon();
        if (guard.Items.IsFull && current == null)
            return null;
        var stored = guard.Location.Rooms
            .SelectMany(room => room.Items.Select(item => new { Room = room, Item = item }))
            .Where(entry => IsMeleeWeapon(entry.Item) && allowed(entry.Item.Type) &&
                entry.Item.Type.WeaponPriority > (current?.Type.WeaponPriority ?? 0) &&
                (entry.Item.Type.Production == null ||
                    entry.Item.Type.Production != guard.Location.Production ||
                    current?.Type.Production == entry.Item.Type.Production ||
                    guard.Location.GetProductionToolCount(entry.Item.Type.Production) >
                        entry.Item.Type.Production.MaxToolCount))
            .OrderByDescending(entry => entry.Item.Type.WeaponPriority)
            .FirstOrDefault();
        if (stored == null)
            return null;

        stored.Room.Items.Remove(stored.Item);
        if (current != null)
        {
            guard.Items.Remove(current);
            stored.Room.Items.Add(current);
        }
        guard.Items.Add(stored.Item);
        guard.Weapon = stored.Item;
        return stored.Item;
    }

    internal static void RefreshWeapons(IReadOnlyCollection<Character> unit)
    {
        foreach (Character character in unit)
            character.Weapon = character.Items.FindBestWeapon(character.Weapon);
    }

    internal static bool WeaponAllowed(ClassicAiState state, IReadOnlyCollection<Character> unit,
        Character recipient, ItemType type) => !AiItemPool.IsFirearm(type) ||
        EquipmentPlanning.CanAssignFirearm(recipient, unit, type, AiPolicy.ForDifficulty(state.Difficulty));
}
