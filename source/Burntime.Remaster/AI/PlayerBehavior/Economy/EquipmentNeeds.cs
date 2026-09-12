using System;
using System.Linq;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.AI;

// A recipient replaces a purchasing quota: assigned equipment does not count
// against another character's needs. Only unused stock offsets demand.
internal sealed class EquipmentNeeds
{
    readonly ClassicAiState state;
    readonly AiPolicy policy;
    readonly Character[] recipients;

    internal EquipmentNeeds(ClassicAiState state)
    {
        this.state = state;
        policy = AiPolicy.ForDifficulty(state.Difficulty);
        recipients = EquipmentPlanning.Recipients(state).ToArray();
    }

    internal static bool IsEquipment(ItemType type) =>
        type.DamageValue > 0 || type.DefenseValue > 0 || AiItemPool.IsFirearm(type) || type.ID == "item_ammunition";

    internal int Demand(ItemType type)
    {
        if (type.ID == "item_ammunition")
        {
            int guns = recipients.Sum(character => character.Items.Count(item => AiItemPool.IsFirearm(item.Type)));
            int ammunition = recipients.Sum(character => character.Items.Count(item => item.ID == type.ID)) +
                state.Reserve.GetConstructionMaterialCount(type.ID);
            return Math.Max(0, guns - ammunition);
        }
        if (type.DamageValue <= 0 && type.DefenseValue <= 0)
            return 0;
        bool armour = type.DefenseValue > 0;
        if (armour && (state.RootGame.Rules != RuleSet.Extended || type.DefenseValue > policy.ArmourLimit))
            return 0;
        bool firearm = AiItemPool.IsFirearm(type);
        int wanted = recipients.Count(character =>
        {
            if (firearm && !state.Player.Group.Contains(character))
                return false;
            Item? current = armour ? character.Items.FindBestDefense() : character.Items.FindBestWeapon();
            if (character.Items.IsFull && current == null)
                return false;
            if (armour ? (current?.DefenseValue ?? 0) >= type.DefenseValue :
                (current?.Type.WeaponPriority ?? 0) >= type.WeaponPriority)
                return false;
            if (firearm && !EquipmentPlanning.CanAssignFirearm(character, state.Player.Group.ToArray(), type, policy))
                return false;
            // An unarmed character needs a basic weapon. Better equipment and
            // firearms are optional opportunities, with stronger Easy restraint.
            int chance = firearm ? policy.FirearmPurchaseChance :
                !armour && current == null && (state.Difficulty > 0 || type.ID == "item_knife")
                    ? 100 : policy.EquipmentUpgradeChance;
            return Opportunity(state.RootGame.World.Day, state.Player.Index, type.ID, chance);
        });
        if (firearm && policy.FirearmLimit >= 0)
            wanted = Math.Min(wanted, Math.Max(0, policy.FirearmLimit -
                state.Player.Group.Count(character => character.Items.Any(item => AiItemPool.IsFirearm(item.Type)))));
        int reserve = state.Reserve.GetContents().Where(entry => entry.Type == type).Sum(entry => entry.Count);
        int spare = recipients.Sum(character => character.Items.Count(item => item.Type == type &&
            item != (armour ? character.Items.FindBestDefense() : character.Items.FindBestWeapon())));
        return Math.Max(0, wanted - reserve - spare);
    }

    internal static bool Opportunity(int day, int player, string id, int chance)
    {
        uint hash = unchecked((uint)(day * 397 + player * 7919));
        foreach (char value in id)
            hash = unchecked((hash ^ value) * 16777619);
        return hash % 100 < Math.Clamp(chance, 0, 100);
    }
}
