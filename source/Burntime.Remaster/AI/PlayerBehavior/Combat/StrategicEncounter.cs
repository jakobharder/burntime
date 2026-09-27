using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.AI;

internal static class StrategicEncounter
{
    internal const int AttackingBossSafetyHealth = 40;
    internal const int DefendingBossSafetyHealth = 65;
    const int MaxRounds = 100;

    internal readonly record struct Result(bool AttackerWithdrew, bool DefendingPartyDisengaged);

    internal static Result Fight(ClassicGame game, Player attacker, Player defenderOwner,
        IReadOnlyCollection<Character> defenders, bool fightToDeath)
    {
        Character[] originalAttackers = attacker.Party.ToArray();
        var startingHealth = originalAttackers.ToDictionary(character => character, character => character.Health);
        HashSet<Character> visitingParty = defenders.Where(defenderOwner.Party.Contains).ToHashSet();
        bool defendingPartyDisengaged = false;
        IEnumerable<Character> LivingDefenders() => defenders.Where(character => !character.IsDead &&
            (!defendingPartyDisengaged || !visitingParty.Contains(character)));
        bool DisengageDefender(Character? opponent = null)
        {
            if (!visitingParty.Contains(defenderOwner.Character))
                return false;
            if (NeedsWithdrawal(game, defenderOwner.Character, opponent, DefendingBossSafetyHealth))
                defendingPartyDisengaged = true;
            return defendingPartyDisengaged;
        }

        DisengageDefender();
        for (int round = 0; round < MaxRounds; round++)
        {
            if (!LivingDefenders().Any() || attacker.Character.IsDead)
                break;
            if (!fightToDeath && (NeedsWithdrawal(game, attacker.Character, null, AttackingBossSafetyHealth) ||
                !attacker.Party.Any(character => character != attacker.Character)))
                return new(true, defendingPartyDisengaged);

            int defendersBeforeRound = LivingDefenders().Count();
            foreach (Character fighter in attacker.Party.ToArray())
            {
                if (fighter.IsDead || attacker.Character.IsDead)
                    break;
                Character? target;
                while (true)
                {
                    target = LivingDefenders().OrderBy(character => character.Health).FirstOrDefault();
                    if (target == null)
                        break;
                    if (target == defenderOwner.Character && DisengageDefender(fighter))
                        continue;
                    break;
                }
                if (target == null)
                    break;
                if (!fightToDeath && fighter == attacker.Character &&
                    NeedsWithdrawal(game, fighter, target, AttackingBossSafetyHealth))
                    return new(true, defendingPartyDisengaged);

                DealDamage(game, fighter, target);
                // Match local combat: a living target immediately retaliates,
                // including against the attacking boss. Dead targets never act.
                if (!target.IsDead)
                    DealDamage(game, target, fighter);
                DisengageDefender();
                if (!fightToDeath && (NeedsWithdrawal(game, attacker.Character, null, AttackingBossSafetyHealth) ||
                    originalAttackers.Any(character => character != attacker.Character &&
                        (character.IsDead || character.Health <= 35 && character.Health < startingHealth[character]))))
                    return new(true, defendingPartyDisengaged);
            }
            if (!fightToDeath && LivingDefenders().Any() && LivingDefenders().Count() < defendersBeforeRound)
                return new(true, defendingPartyDisengaged);
        }
        return new(false, defendingPartyDisengaged);
    }

    internal static bool NeedsWithdrawal(ClassicGame game, Character boss, Character? opponent, int safetyHealth)
    {
        if (boss.Health <= safetyHealth)
            return true;
        if (opponent == null)
            return false;
        int maximum = game.RuleBook.GetCombatPreview(opponent).Maximum;
        int protection = game.RuleBook.GetCombatPreview(boss).Defense ?? 0;
        return boss.Health - RuleFormulas.ApplyArmour(maximum, protection) < safetyHealth;
    }

    static void DealDamage(ClassicGame game, Character attacker, Character defender)
    {
        int damage = game.RuleBook.RollStrategicDamage(game, attacker.Player!, attacker, defender);
        if (damage > 0)
            defender.Health -= damage;
    }
}
