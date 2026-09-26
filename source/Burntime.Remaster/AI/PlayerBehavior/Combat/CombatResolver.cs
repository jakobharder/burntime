using System.Collections.Generic;
using System.Linq;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.AI;

internal static class CombatResolver
{
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

        List<Character> originalDefenders = CombatStrength.Defenders(location)
            .ToList();
        List<Character> originalAttackers = attacker.Party.ToList();
        Dictionary<Character, Item[]> carriedByDefenders = originalDefenders
            .ToDictionary(character => character, character => character.Items.ToArray());
        Dictionary<Character, Item[]> carriedBeforeCombat = originalAttackers
            .ToDictionary(character => character, character => character.Items.ToArray());
        float initialAttackerStrength = CombatStrength.Attacker(attacker);
        float initialDefenderStrength = CombatStrength.AssessedDefenders(
            location, AiPolicy.ForDifficulty(state.Difficulty));
        DefenseIntelligence.UpdateKnowledgeFromEncounter(state, location, originalDefenders);
        AiTelemetry.Report(attacker,
            $"attacks {defenderOwner.Name}'s camp at {location.Title}: " +
            $"{attacker.Party.Count} attackers against {originalDefenders.Count} defenders");

        var encounter = StrategicEncounter.Fight(state.RootGame, attacker, defenderOwner,
            originalDefenders, fightToDeath);
        bool tacticalWithdrawal = encounter.AttackerWithdrew;
        if (encounter.DefendingPartyDisengaged)
            AiTelemetry.Report(defenderOwner, $"disengaged defending party at {location.Title} to protect the boss; party stays at the location");

        foreach (Character casualty in originalDefenders.Where(character => character.IsDead))
            AiTelemetry.Report(attacker, $"defeated defender {casualty.Name} at {location.Title}");
        foreach (Character casualty in originalAttackers.Where(character => character.IsDead && character != attacker.Character))
            AiTelemetry.Report(attacker, $"lost follower {casualty.Name} in the attack on {location.Title}");

        Item[] ownDrops = originalAttackers
            .Where(character => character.IsDead && character != attacker.Character)
            .SelectMany(character => carriedBeforeCombat[character])
            .ToArray();
        state.CollectCombatLoot(ownDrops);

        Character[] survivingDefenders = originalDefenders.Where(character => !character.IsDead &&
            (!encounter.DefendingPartyDisengaged || !defenderOwner.Party.Contains(character))).ToArray();
        bool defendersDefeated = survivingDefenders.Length == 0;
        DefenseIntelligence.UpdateKnowledgeFromEncounter(state, location, survivingDefenders);
        if (defendersDefeated)
        {
            Item[] defenderDrops = originalDefenders
                .Where(character => character.IsDead)
                .SelectMany(character => carriedByDefenders[character])
                .Where(item => location.Items.Any(ground => ground == item))
                .ToArray();
            state.CollectCombatLoot(SelectDefenderLoot(defenderDrops,
                AiPolicy.ForDifficulty(state.Difficulty).CombatLootLimit));

            state.LastChanceAttackTarget = null;
            Character? guard = attacker.Party
                .Where(character => character != attacker.Character && !character.IsDead)
                .OrderBy(character => CombatStrength.Fighter(character))
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

    internal static IEnumerable<Item> SelectDefenderLoot(IEnumerable<Item> dropped, int limit) =>
        dropped
            .Where(item => EquipmentNeeds.IsEquipment(item.Type))
            .OrderByDescending(item => item.Type.WeaponPriority)
            .ThenByDescending(item => item.DefenseValue)
            .ThenByDescending(item => item.TradeValue)
            .Take(System.Math.Max(0, limit));

}
