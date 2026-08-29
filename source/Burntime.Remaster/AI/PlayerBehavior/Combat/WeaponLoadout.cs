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
        string role)
    {
        Item current = character.Items.FindBestWeapon();
        int currentDamage = current?.DamageValue ?? 0;
        int desiredMinimum = upgradeWeakWeapon && currentDamage < 33
            ? currentDamage
            : currentDamage > 0 ? int.MaxValue : 0;
        bool reserveProductionTool = ExpansionPlanning.ShouldReserveProductionTool(state);
        bool allowProductionTool = currentDamage == 0 || !reserveProductionTool;
        bool Allowed(ItemType type) => WeaponAllowed(state, unit, character, type);
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
                state.Reserve.Insert(weapon);
                return;
            }
            character.Items.Remove(replaceable);
            state.Current.Items.Add(replaceable);
            AiTelemetry.Report(state.Player,
                $"dropped lower-value cargo {replaceable.ID} so {character.Name} can carry a weapon");
        }
        if (!character.Items.Add(weapon))
        {
            state.Reserve.Insert(weapon);
            return;
        }

        character.Weapon = weapon;
        string location = character.IsStationed ? $" at {character.Location.Title}" : "";
        AiTelemetry.Report(state.Player,
            $"equipped {role} {character.Name}{location} with {weapon.ID}");
    }

    internal static void NormalizeWeaponLimits(
        ClassicAiState state,
        IReadOnlyCollection<Character> unit)
    {
        const int firearmLimit = 0;
        int pitchforkLimit = AiPolicy.ForDifficulty(state.Difficulty).PitchforkLimit;
        int firearms = 0;
        int pitchforks = 0;

        foreach (Character character in unit.OrderByDescending(member => member.Experience))
        {
            foreach (Item weapon in character.Items.Where(item =>
                AiItemPool.IsFirearm(item.Type) || item.ID == "item_pitchfork").ToArray())
            {
                bool allowed = AiItemPool.IsFirearm(weapon.Type)
                    ? firearms++ < firearmLimit
                    : pitchforks++ < pitchforkLimit;
                if (allowed)
                    continue;

                if (character.Weapon == weapon)
                    character.Weapon = null;
                character.Items.Remove(weapon);
                CampManagement.StoreInReserveOrAtLocation(state, weapon, character.Location);
                AiTelemetry.Report(state.Player,
                    $"reserved restricted weapon {weapon.ID} carried by {character.Name}");
            }
            character.Weapon = character.Items.FindBestWeapon(character.Weapon);
        }
    }

    internal static bool WeaponAllowed(
        ClassicAiState state,
        IReadOnlyCollection<Character> unit,
        Character recipient,
        ItemType type)
    {
        if (AiItemPool.IsFirearm(type))
            return false;
        if (type.ID != "item_pitchfork")
            return true;
        int pitchforkLimit = AiPolicy.ForDifficulty(state.Difficulty).PitchforkLimit;
        if (pitchforkLimit <= 0)
            return false;
        return unit.Where(character => character != recipient)
            .SelectMany(character => character.Items)
            .Count(item => item.ID == "item_pitchfork") < pitchforkLimit;
    }
}
