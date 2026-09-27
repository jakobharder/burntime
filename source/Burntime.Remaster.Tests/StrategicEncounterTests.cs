using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster;
using Burntime.Remaster.AI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.Tests;

using static Program;

static class StrategicEncounterTests
{
    internal static IEnumerable<Case<int>> StrategicEncounterCases()
    {
        foreach (int startingHealth in new[] { 30, 35, 36 })
            yield return Int($"follower starting at {startingHealth} health withdraws only after new injury", 0, () =>
            {
                var (game, attacker, defender, manager) = EncounterPlayers(RuleSet.Extended);
                var guard = EncounterFighter(manager, defender, 100, 1);
                var follower = EncounterFighter(manager, attacker, startingHealth, 10);
                attacker.Party.Add(follower);
                var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender, new[] { guard }, false);
                Equal(89, guard.Health, "injured follower gets its attack; boss exchange alone does not cause withdrawal");
                Equal(startingHealth - 1, follower.Health, "new retaliation damage triggers safety rule");
                Equal(true, result.AttackerWithdrew, "withdraw after fresh injury at or below threshold");
                return 0;
            });
        yield return Int("raid still withdraws after eliminating a defender with an injured follower along", 0, () =>
        {
            var (game, attacker, defender, manager) = EncounterPlayers(RuleSet.Extended);
            var weakGuard = EncounterFighter(manager, defender, 2, 1);
            var otherGuard = EncounterFighter(manager, defender, 100, 1);
            var follower = EncounterFighter(manager, attacker, 30, 10);
            attacker.Party.Add(follower);
            var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender,
                new[] { weakGuard, otherGuard }, false);
            Equal(true, weakGuard.IsDead, "raid can make progress despite old injury");
            Equal(100, otherGuard.Health, "successful raid still disengages before another round");
            Equal(30, follower.Health, "old injury alone does not abort the round");
            Equal(true, result.AttackerWithdrew, "raid-and-return behavior preserved");
            return 0;
        });
        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
        {
            yield return Int($"{rule} immediate retaliation protects attacking boss", 0, () =>
            {
                var (game, attacker, defender, manager) = EncounterPlayers(rule);
                var guard = EncounterFighter(manager, defender, 100, 60);
                var follower = EncounterFighter(manager, attacker, 100, 1);
                attacker.Party.Add(follower);
                var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender,
                    new[] { guard }, false);
                Equal(40, attacker.Character.Health, "boss receives immediate retaliation");
                Equal(99, guard.Health, "retreat happens before follower's attack");
                Equal(100, follower.Health, "follower not used as an invulnerable boss's shield");
                Equal(true, result.AttackerWithdrew, "boss orders retreat at safety threshold");
                return 0;
            });
            yield return Int($"{rule} defending party disengages before serious injury", 0, () =>
            {
                var (game, attacker, defender, manager) = EncounterPlayers(rule);
                attacker.Character.Items.Clear();
                attacker.Character.Items.Add(TestItem(manager, "item_knife", damage: 40, damageValues: new[] { 40 }));
                attacker.Party.Add(EncounterFighter(manager, attacker, 100, 1));
                var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender,
                    new[] { defender.Character }, false);
                Equal(true, result.DefendingPartyDisengaged, "party disengages before a hit could cross 65");
                Equal(100, defender.Character.Health, "no serious injury");
                Equal(false, defender.IsTraveling, "disengagement alone does not move the party");
                return 0;
            });
            yield return Int($"{rule} last-chance boss can die", 0, () =>
            {
                var (game, attacker, defender, manager) = EncounterPlayers(rule);
                var guard = EncounterFighter(manager, defender, 100, 100);
                var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender,
                    new[] { guard }, true);
                Equal(true, attacker.Character.IsDead, "last-chance boss accepts lethal retaliation");
                Equal(false, result.AttackerWithdrew, "last chance does not retreat");
                return 0;
            });
            yield return Int($"{rule} disengaged defending party stays when guards hold", 0, () =>
            {
                var (game, attacker, defender, manager) = EncounterPlayers(rule);
                defender.Character.Health = 65;
                var guard = EncounterFighter(manager, defender, 100, 60);
                attacker.Party.Add(EncounterFighter(manager, attacker, 100, 1));
                var result = Burntime.Remaster.AI.StrategicEncounter.Fight(game, attacker, defender,
                    new[] { guard, defender.Character }, false);
                Equal(true, result.DefendingPartyDisengaged, "low-health boss disengages");
                Equal(true, result.AttackerWithdrew, "guards repel attack");
                Equal(65, defender.Character.Health, "disengaged boss takes no further damage");
                Equal(false, defender.IsTraveling, "party stays while camp holds");
                return 0;
            });
        }
    }
}
