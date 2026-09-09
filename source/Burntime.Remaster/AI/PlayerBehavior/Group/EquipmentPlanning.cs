using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.AI;

internal static class EquipmentPlanning
{
    internal static IEnumerable<Character> Recipients(ClassicAiState state) =>
        state.Player.Group.Where(character => !character.IsDead)
            .Concat(state.RootGame.World.Locations.Where(camp => camp.Player == state.Player &&
                    ReinforcementPlanning.CalculateIsThreatened(state, camp))
                .SelectMany(camp => camp.CampNPC.Where(character => character.Player == state.Player && !character.IsDead))
                .OrderByDescending(character => character.Experience))
            .Distinct();

    internal static int LoadedShots(ItemType type, int? currentAmmo = null)
    {
        int shots = 0;
        HashSet<ItemType> seen = new();
        while (type != null && type.AmmoValue > 0 && seen.Add(type))
        {
            shots += currentAmmo ?? type.AmmoValue;
            currentAmmo = null;
            type = type.Empty;
        }
        return shots;
    }

    internal static bool CanAssignFirearm(Character recipient, IReadOnlyCollection<Character> unit,
        ItemType type, AiPolicy policy) => LoadedShots(type) > 0 &&
        (policy.FirearmLimit < 0 || unit.Count(character => character != recipient &&
            character.Items.Any(item => AiItemPool.IsFirearm(item.Type))) < policy.FirearmLimit);

    // Transfer physical guns, including their remaining ammunition. Existing
    // owners retain their best gun even if they exceed a purchasing preference.
    internal static void DistributeFirearms(IReadOnlyCollection<Character> party, AiPolicy policy)
    {
        foreach (Character recipient in party)
        {
            Item? current = recipient.Items.FindBestWeapon();
            if (recipient.Items.IsFull && current == null)
                continue;
            var candidate = party.SelectMany(owner => owner.Items.Select(item => new { Owner = owner, Item = item }))
                .Where(entry => entry.Owner != recipient && AiItemPool.IsFirearm(entry.Item.Type) &&
                    entry.Item != entry.Owner.Items.FindBestWeapon() &&
                    entry.Item.Type.WeaponPriority > (current?.Type.WeaponPriority ?? 0) &&
                    LoadedShots(entry.Item.Type, entry.Item.AmmoValue) > 0 &&
                    CanAssignFirearm(recipient, party, entry.Item.Type, policy))
                .OrderByDescending(entry => entry.Item.Type.WeaponPriority).FirstOrDefault();
            if (candidate == null)
                continue;
            candidate.Owner.Items.Remove(candidate.Item);
            if (current != null)
            {
                recipient.Items.Remove(current);
                candidate.Owner.Items.Add(current);
            }
            recipient.Items.Add(candidate.Item);
            recipient.Weapon = candidate.Item;
            candidate.Owner.Weapon = candidate.Owner.Items.FindBestWeapon();
        }
    }

    internal static void DistributeClothing(IReadOnlyList<Character> recipients, int armourLimit)
    {
        for (int index = 0; index < recipients.Count; index++)
        {
            Character recipient = recipients[index];
            Item? current = recipient.Items.FindBestDefense();
            if (recipient.Items.IsFull && current == null) continue;
            var candidate = recipients.Skip(index + 1)
                .SelectMany(owner => owner.Items.Select(item => new { Owner = owner, Item = item }))
                .Where(entry => entry.Item.DefenseValue > (current?.DefenseValue ?? 0) &&
                    entry.Item.DefenseValue <= armourLimit)
                .OrderByDescending(entry => entry.Item.DefenseValue).FirstOrDefault();
            if (candidate == null) continue;
            bool wasProtection = candidate.Owner.Protection == candidate.Item;
            candidate.Owner.Items.Remove(candidate.Item);
            if (current != null)
            {
                recipient.Items.Remove(current);
                candidate.Owner.Items.Add(current);
            }
            recipient.Items.Add(candidate.Item);
            recipient.Protection = candidate.Item;
            if (wasProtection)
                candidate.Owner.Protection = candidate.Owner.Items.FindBestDefense();
        }
    }

    internal static void Maintain(ClassicAiState state)
    {
        AiPolicy policy = AiPolicy.ForDifficulty(state.Difficulty);
        Character[] recipients = Recipients(state).ToArray();
        DistributeFirearms(state.Player.Group.Where(character => !character.IsDead).ToArray(), policy);
        foreach (Character character in recipients)
        {
            // Ammunition remains a physical item and is consumed by the normal
            // combat reload path. Also reload guns picked up already empty.
            Item? gun = character.Items.FirstOrDefault(item => AiItemPool.IsFirearm(item.Type));
            if (gun != null)
            {
                Item? ammunition = character.Items.FirstOrDefault(item => item.ID == "item_ammunition");
                if (ammunition == null && !character.Items.IsFull)
                {
                    Character? donor = state.Player.Group.FirstOrDefault(member => member != character && member.Location == character.Location &&
                        !member.Items.Any(item => AiItemPool.IsFirearm(item.Type)) &&
                        member.Items.Any(item => item.ID == "item_ammunition"));
                    ammunition = donor?.Items.First(item => item.ID == "item_ammunition");
                    if (ammunition != null)
                    {
                        donor!.Items.Remove(ammunition);
                        character.Items.Add(ammunition);
                    }
                    else if (state.Reserve.GetConstructionMaterialCount("item_ammunition") > 0)
                    {
                        ammunition = state.Reserve.TakeForTrade(state.RootGame.ItemTypes["item_ammunition"]);
                        if (ammunition != null) character.Items.Add(ammunition);
                    }
                }
                if (gun.DamageValue == 0 && ammunition != null)
                {
                    character.Items.Remove(ammunition);
                    gun.Reload(state.RootGame.ItemTypes[gun.ID == "item_unloaded_pistol"
                        ? "item_loaded_pistol" : "item_loaded_rifle"]);
                }
            }
            character.Weapon = character.Items.FindBestWeapon();
        }
        if (state.RootGame.Rules != RuleSetId.Extended)
            return;

        // Pool spare clothing so upgrades can pass down to another recipient.
        foreach (Character character in recipients)
        {
            Item? keep = character.Items.Where(item => item.DefenseValue > 0 && item.DefenseValue <= policy.ArmourLimit)
                .OrderByDescending(item => item.DefenseValue).FirstOrDefault();
            foreach (Item spare in character.Items.Where(item => item.DefenseValue > 0 && item != keep).ToArray())
            {
                if (character.Protection == spare) character.Protection = keep;
                character.Items.Remove(spare);
                CampManagement.StoreInReserveOrAtLocation(state, spare, character.Location);
            }
        }
        // Collect stored clothing through the same shared reserve as other gear.
        foreach (Location camp in recipients.Select(character => character.Location).Distinct()
            .Where(camp => camp != null && camp.Player == state.Player))
            foreach (var entry in camp.Rooms.SelectMany(room => room.Items
                .Where(item => item.DefenseValue > 0 && item.DefenseValue <= policy.ArmourLimit)
                .Select(item => new { Room = room, Item = item })).ToArray())
                if (state.Reserve.Insert(entry.Item)) entry.Room.Items.Remove(entry.Item);

        DistributeClothing(recipients, policy.ArmourLimit);
        foreach (Character character in recipients)
        {
            Item? current = character.Items.FindBestDefense();
            if (character.Items.IsFull && current == null)
                continue;
            ItemType? upgrade = state.Reserve.GetContents().Where(entry => entry.Count > 0 &&
                    entry.Type.DefenseValue > (current?.DefenseValue ?? 0) && entry.Type.DefenseValue <= policy.ArmourLimit)
                .OrderByDescending(entry => entry.Type.DefenseValue).Select(entry => entry.Type).FirstOrDefault();
            if (upgrade == null)
                continue;
            Item? clothing = state.Reserve.TakeForTrade(upgrade);
            if (clothing == null) continue;
            if (current != null)
            {
                character.Items.Remove(current);
                CampManagement.StoreInReserveOrAtLocation(state, current, character.Location);
            }
            character.Items.Add(clothing);
            character.Protection = clothing;
            AiTelemetry.Report(state.Player, $"equipped {character.Name} with {clothing.ID} ({clothing.DefenseValue}% armour)");
        }
    }
}
