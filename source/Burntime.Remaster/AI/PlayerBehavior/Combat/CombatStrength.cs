using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class CombatStrength
{
    public static float Fighter(Character character) => Fighter(character, detailed: true);

    static float Fighter(Character character, bool detailed) =>
        ((ClassicGame)character.Container.Root).RuleBook.CalculateStrategicStrength(
            character, detailed);

    public static float Attacker(Player player) => player.Group
        .Where(character => !character.IsDead)
        .Sum(Fighter);

    internal static System.Collections.Generic.IEnumerable<Character> Defenders(Location location) =>
        location.Player == null ? Enumerable.Empty<Character>() :
            AiStateOperations.GetCampDefenders(location, null, new[] { location.Player });

    public static float AssessedDefenders(Location location, AiPolicy policy)
    {
        Character[] defenders = Defenders(location).ToArray();
        return defenders.Sum(character => Fighter(character, policy.UseDetailedCombatEstimate));
    }
}
