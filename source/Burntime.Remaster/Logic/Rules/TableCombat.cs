using System;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Logic.Rules;

internal static class TableCombat
{
    static int[] DamageValues(Character character, Item? weapon, GameSettings settings)
    {
        if (character.Class is CharClass.Trader or CharClass.Mutant or CharClass.Dog)
        {
            int difficulty = character.Container.Root is ClassicGame game && game.World is not null
                ? game.World.Difficulty
                : 1;
            return character.Class switch
            {
                CharClass.Trader => settings.GetTraderAttack(difficulty),
                CharClass.Mutant => settings.GetMutantAttack(difficulty),
                _ => settings.GetDogAttack(difficulty),
            };
        }
        return weapon?.Type.DamageValues is { Length: > 0 } damage ? damage :
            ((ClassicGame)character.Container.Root).ItemTypes.UnarmedDamage;
    }

    internal static int ProtectionPercent(Character character) =>
        Math.Clamp(character.Items.FindBestDefense(character.Protection)?.DefenseValue ?? 0, 0, 100);

    internal static CombatPreview Preview(Character character, GameSettings settings, bool armour)
    {
        var preview = RuleFormulas.OriginalCombatPreview(
            DamageValues(character, character.FindOriginalWeapon(), settings),
            character.CombatExperience(settings), settings.CombatTierWidth);
        int protection = armour ? ProtectionPercent(character) : 0;
        return preview with { Defense = protection > 0 ? protection : null };
    }

    internal static float Strength(Character character, GameSettings settings, bool detailed, bool armour)
    {
        float average = RuleFormulas.OriginalStrategicStrength(
            DamageValues(character, character.FindOriginalWeapon(), settings),
            character.CombatExperience(settings), settings.CombatTierWidth, 0, detailed: true);
        float health = detailed ? character.Health / 10f : 10f;
        if (armour)
            health /= Math.Max(0.01f, 1 - ProtectionPercent(character) / 100f);
        return average + health;
    }

    internal static int Roll(Character attacker, Character? defender, GameSettings settings,
        bool useAmmo, bool armour)
    {
        Item? weapon = attacker.Class is CharClass.Trader or CharClass.Mutant or CharClass.Dog
            ? null
            : attacker.SelectOriginalWeapon();
        int damage = RuleFormulas.OriginalDamage(DamageValues(attacker, weapon, settings),
            attacker.CombatExperience(settings), settings.CombatTierWidth,
            Burntime.Platform.Math.Random.Next(0, 4));
        if (weapon?.ConsumesAmmo == true && useAmmo)
            attacker.UseOriginalWeapon(weapon);
        if (armour && defender != null)
        {
            defender.Protection = defender.Items.FindBestDefense(defender.Protection);
            damage = RuleFormulas.ApplyArmour(damage, ProtectionPercent(defender));
        }
        return damage;
    }
}
