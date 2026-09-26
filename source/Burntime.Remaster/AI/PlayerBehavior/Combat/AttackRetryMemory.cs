using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

// Runtime knowledge, like DefenseIntelligence. Never inspect remote defenders:
// only compare forces actually observed at the start of an encounter.
internal sealed class AttackRetryMemory
{
    static readonly ConditionalWeakTable<Player, AttackRetryMemory> Memories = new();
    readonly Dictionary<Location, Attempt> attempts = new();
    internal static AttackRetryMemory For(Player player) => Memories.GetOrCreateValue(player);

    internal sealed record Force(int Count, int Health, float Strength, int BossHealth)
    {
        internal static Force Capture(IEnumerable<Character> characters, Character? boss = null)
        {
            Character[] living = characters.Where(character => !character.IsDead).ToArray();
            return new(living.Length, living.Sum(character => character.Health),
                living.Sum(CombatStrength.Fighter), boss?.Health ?? 0);
        }

        internal bool ImprovedOver(Force previous) => Count > previous.Count ||
            Strength >= previous.Strength * 1.10f || BossHealth >= previous.BossHealth + 10;

        internal bool WeakerThan(Force previous) =>
            Count < previous.Count || Count == previous.Count &&
            ((Health < previous.Health && Strength <= previous.Strength) ||
             Strength < previous.Strength * 0.85f);
    }

    sealed record Attempt(Player Owner, Force Attackers, Force Defenders, bool Blocked);
    internal readonly record struct Assessment(string Comparison, bool Blocked);

    internal Assessment Record(Location target, Player owner, Force attackers, Force defenders,
        int killed, bool captured)
    {
        if (captured)
        {
            attempts.Remove(target);
            return new("defense cleared", false);
        }

        bool comparable = attempts.TryGetValue(target, out Attempt? previous) && previous.Owner == owner;
        bool lasting = comparable && defenders.WeakerThan(previous!.Defenders);
        bool improved = comparable && attackers.ImprovedOver(previous!.Attackers);
        bool blocked = comparable && !lasting && !improved && killed == 0;
        string comparison = killed > 0 ? "defender eliminated" : !comparable ? "first encounter; lasting progress unknown" :
            lasting ? "defense weaker than at previous encounter start" :
            improved ? "attacking party improved" : "no lasting progress since previous encounter start";
        if (comparable)
            comparison += $" (starting defense {previous!.Defenders.Count} defenders/{previous.Defenders.Health} HP " +
                $"-> {defenders.Count} defenders/{defenders.Health} HP)";
        attempts[target] = new(owner, attackers, defenders, blocked);
        return new(comparison, blocked);
    }

    internal bool CanRetry(Location target, Player owner, Force attackers, Force? observedDefenders = null)
    {
        if (!attempts.TryGetValue(target, out Attempt? previous) || previous.Owner != owner || !previous.Blocked)
            return true;
        // No day/cooldown exception: time alone cannot solve a demonstrated stalemate.
        return attackers.ImprovedOver(previous.Attackers) ||
            observedDefenders?.WeakerThan(previous.Defenders) == true;
    }

    internal bool CanRetry(Player player, Location target) =>
        target.Player == null || CanRetry(target, target.Player,
            Force.Capture(player.Party, player.Character),
            player.Location == target ? Force.Capture(CombatStrength.Defenders(target)) : null);
}
