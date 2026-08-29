using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class CombatStrength
{
    public static float Attacker(Player player) => player.Group
        .Where(character => !character.IsDead)
        .Sum(character => character.AttackValue + character.DefenseValue + character.Health / 10f);

    public static float AssessedDefenders(Location location, AiPolicy policy)
    {
        Character[] defenders = location.CampNPC
            .Where(character => !character.IsDead && character.Player == location.Player)
            .ToArray();
        return policy.UseDetailedCombatEstimate
            ? defenders.Sum(character =>
                character.AttackValue + character.DefenseValue + character.Health / 10f)
            : defenders.Sum(character =>
                (character.Items.FindBestWeapon()?.DamageValue ?? character.BaseAttackValue) + 10f);
    }
}
