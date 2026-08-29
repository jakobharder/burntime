using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class CombatResolver
{
    const int MaxRounds = 100;

    public static void Resolve(ClassicAiState state, bool fightToDeath = false)
    {
        Player attacker = state.Player;
        Location location = state.Current;
        Player? defenderOwner = location.Player;
        if (defenderOwner == null || defenderOwner == attacker || location.IsCity)
        {
            state.StrategicTarget = null;
            return;
        }

        List<Character> originalDefenders = location.CampNPC
            .Where(character => character.Player == defenderOwner && !character.IsDead)
            .ToList();
        List<Character> originalAttackers = attacker.Group.Where(character => !character.IsDead).ToList();
        Dictionary<Character, Item[]> carriedBeforeCombat = originalAttackers
            .ToDictionary(character => character, character => character.Items.ToArray());
        float initialAttackerStrength = CombatStrength.Attacker(attacker);
        float initialDefenderStrength = CombatStrength.AssessedDefenders(
            location, AiPolicy.ForDifficulty(state.Difficulty));
        DefenseIntelligence.UpdateKnowledgeFromEncounter(state, location, originalDefenders);
        AiTelemetry.Report(attacker,
            $"attacks {defenderOwner.Name}'s camp at {location.Title}: " +
            $"{attacker.Group.Count} attackers against {originalDefenders.Count} defenders");

        bool tacticalWithdrawal = false;
        for (int round = 1; round <= MaxRounds; round++)
        {
            List<Character> defenders = originalDefenders.Where(character => !character.IsDead).ToList();
            if (defenders.Count == 0)
                break;

            if (!fightToDeath && !attacker.Group.Any(character => character != attacker.Character && !character.IsDead))
                break;

            int defendersBeforeRound = defenders.Count;
            foreach (Character fighter in attacker.Group.Where(character => !character.IsDead).ToArray())
            {
                Character? target = defenders.Where(character => !character.IsDead)
                    .OrderBy(character => character.Health)
                    .FirstOrDefault();
                if (target == null)
                    break;
                DealDamage(fighter, target);
            }

            defenders = originalDefenders.Where(character => !character.IsDead).ToList();
            foreach (Character fighter in defenders)
            {
                Character? target = attacker.Group
                    .Where(character => !character.IsDead &&
                        (fightToDeath || character != attacker.Character))
                    .OrderBy(character => character.Health)
                    .FirstOrDefault();
                if (target == null)
                    break;
                DealDamage(fighter, target);
            }

            bool killedDefender = defenders.Count < defendersBeforeRound;
            bool lostFollower = originalAttackers.Any(character =>
                character != attacker.Character && character.IsDead);
            bool followerInDanger = attacker.Group.Any(character =>
                character != attacker.Character && !character.IsDead && character.Health <= 35);
            if (!fightToDeath && defenders.Count > 0 && !lostFollower && (killedDefender || followerInDanger))
            {
                tacticalWithdrawal = true;
                break;
            }
        }

        foreach (Character casualty in originalDefenders.Where(character => character.IsDead))
            AiTelemetry.Report(attacker, $"defeated defender {casualty.Name} at {location.Title}");
        foreach (Character casualty in originalAttackers.Where(character => character.IsDead && character != attacker.Character))
            AiTelemetry.Report(attacker, $"lost follower {casualty.Name} in the attack on {location.Title}");

        Item[] ownDrops = originalAttackers
            .Where(character => character.IsDead && character != attacker.Character)
            .SelectMany(character => carriedBeforeCombat[character])
            .ToArray();
        state.CollectCombatLoot(ownDrops);

        bool defendersDefeated = originalDefenders.All(character => character.IsDead);
        Character[] survivingDefenders = originalDefenders.Where(character => !character.IsDead).ToArray();
        DefenseIntelligence.UpdateKnowledgeFromEncounter(state, location, survivingDefenders);
        if (defendersDefeated)
        {
            state.LastChanceAttackTarget = null;
            Character? guard = attacker.Group
                .Where(character => character != attacker.Character && !character.IsDead)
                .OrderBy(character => character.AttackValue + character.DefenseValue)
                .FirstOrDefault();

            location.Player = null;
            if (guard != null)
            {
                state.CreateCamp(guard);
                state.MarkRecentlyCaptured(location, AiPolicy.ForDifficulty(
                    state.Difficulty));
                AiTelemetry.Report(attacker, $"captured {location.Title} and stationed {guard.Name}");
            }
            else
            {
                state.StrategicTarget = null;
                AiTelemetry.Report(attacker,
                    $"won at {location.Title}, but it remains neutral without a surviving follower");
            }
        }
        else
        {
            state.StrategicTarget = null;
            if (!fightToDeath)
                state.LastChanceAttackTarget = null;
            bool madeProgress = survivingDefenders.Length < originalDefenders.Count ||
                survivingDefenders.Any(character => character.Health < 100);
            state.RecordFailedAttack(location, originalAttackers.Count, initialAttackerStrength,
                initialDefenderStrength, AiPolicy.ForDifficulty(state.Difficulty),
                madeProgress);
            if (fightToDeath)
            {
                // Last-chance combat is a binding terminal action. The round
                // limit is only a safety guard and must never turn it into a
                // surviving failed attack that recovery logic can resume from.
                if (!attacker.Character.IsDead)
                    attacker.Character.Health = 0;
                state.LastChanceAttackTarget = null;
                return;
            }
            Location? safeLocation = AiTurnController.FindNearestLogistics(state, requireReachable: true);
            RouteFinder.Route? safeRoute = safeLocation == null
                ? null
                : RouteFinder.Find(attacker, location, safeLocation);
            Location? retreat = safeRoute?.NextStep ?? attacker.PreviousLocation;
            if (retreat != null && attacker.CanTravel(location, retreat))
            {
                attacker.Travel(retreat);
                AiTelemetry.Report(attacker,
                    tacticalWithdrawal && madeProgress
                    ? $"withdrew from {location.Title} after reducing the defense to " +
                        $"{survivingDefenders.Length}, toward {safeLocation?.Title ?? retreat.Title} via {retreat.Title}"
                    : $"retreated from {location.Title} toward " +
                    $"{safeLocation?.Title ?? retreat.Title} via {retreat.Title} before risking the leader");
            }
            else
            {
                AiTelemetry.Report(attacker,
                    $"failed to defeat {location.Title}'s defenders; leader survived");
            }
        }
    }

    static void DealDamage(Character attacker, Character defender)
    {
        int attack = attacker.PrepareStrategicAttack();
        float defense = defender.PrepareStrategicDefense();
        float randomFactor = 0.85f + (float)Burntime.Platform.Math.Random.NextDouble() * 0.30f;
        int damage = (int)System.Math.Max(1, (attack - defense) * randomFactor);
        defender.Health -= damage;
    }
}
