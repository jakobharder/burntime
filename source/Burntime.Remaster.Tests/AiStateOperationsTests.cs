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

static class AiStateOperationsTests
{
    internal static IEnumerable<Case<int>> DefenderCases()
    {
        yield return Int("DOS strategic damage zero boundary", 0, () =>
        {
            var (game, attacker, defender, manager) = EncounterPlayers(RuleSet.Dos);
            // This damage calculation reads only Day; skip the world's UI-singleton initializer.
            game.World = manager.Create(() =>
            {
                var world = (ClassicWorld)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClassicWorld));
                foreach (string field in new[] { "ID", "localID" })
                    typeof(Burntime.Framework.States.StateObject).GetField(field,
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(world, -1);
                return world;
            });
            game.World.Day = 1;
            defender.Character.Experience = 37; // Knife basis 20: strength 57.
            var ai = manager.Create<Burntime.Remaster.AI.DosAiState>(new object[]
            {
                attacker, new Burntime.Remaster.AI.AiSettings { Difficulty = 2 }
            });
            var damage = typeof(Burntime.Remaster.AI.DosAiState).GetMethod("StrategicDamage",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            foreach (var (xp, expected) in new[] { (36, -1), (37, 1), (38, 1), (40, 3) })
            {
                attacker.Character.Experience = xp;
                Equal(expected, (int)damage.Invoke(ai, new object[] { attacker.Character, defender.Character })!,
                    $"damage at boss XP {xp}");
            }
            return 0;
        });
        foreach (string position in new[] { "present", "approaching", "departing", "absent" })
        {
            yield return Int($"DOS owner safeguard: {position}", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var camp = manager.Create<Burntime.Remaster.Logic.Location>();
                var away = manager.Create<Burntime.Remaster.Logic.Location>();
                var attacker = manager.Create<HazardPlayer>(new object[] { 0 });
                var owner = manager.Create<HazardPlayer>(new object[] { 1 });
                attacker.Location = camp;
                camp.Player = owner;
                owner.Location = position is "present" or "departing" ? camp : away;
                owner.SetDestination(position == "approaching" ? camp : away);
                owner.Traveling = position is "approaching" or "departing";
                var guard = manager.Create(() => new HazardCharacter());
                guard.Player = owner;
                guard.Health = 100;
                if (position != "absent")
                    camp.Characters.Add(guard);
                var ai = manager.Create<Burntime.Remaster.AI.DosAiState>(new object[]
                {
                    attacker, new Burntime.Remaster.AI.AiSettings { Difficulty = 2 }
                });
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var resolve = typeof(Burntime.Remaster.AI.DosAiState).GetMethod("ResolveCurrentOpposition", flags)!;
                Equal(false, (bool)resolve.Invoke(ai, null)!, "continues to routing");
                Equal(position != "absent", camp.Player == owner, "protected camp retains ownership, absent owner loses empty camp");
                if (position != "absent")
                {
                    Equal(100, guard.Health, "protected guard takes no strategic damage");
                    Equal(6, (int)typeof(Burntime.Remaster.AI.OriginalAiState).GetField("wait", flags)!.GetValue(ai)!, "resets conflict budget");
                }
                return 0;
            });
        }

        yield return Int("DOS owner-only and Amiga opposing parties", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var camp = manager.Create<Burntime.Remaster.Logic.Location>();
            var attacker = manager.Create<HazardPlayer>(new object[] { 0 });
            var owner = manager.Create<HazardPlayer>(new object[] { 1 });
            var visitor = manager.Create<HazardPlayer>(new object[] { 2 });
            camp.Player = owner;
            owner.Location = camp;
            visitor.Location = camp;
            var guard = manager.Create(() => new HazardCharacter());
            guard.Player = owner; guard.Health = 100;
            camp.Characters.Add(guard);
            var boss = manager.Create(() => new HazardCharacter());
            boss.Player = owner; boss.Health = 100;
            owner.Group.Add(boss);
            var visitingBoss = manager.Create(() => new HazardCharacter());
            visitingBoss.Player = visitor; visitingBoss.Health = 100;
            visitor.Group.Add(visitingBoss);
            var dead = manager.Create(() => new HazardCharacter());
            dead.Player = owner; dead.Health = 0;
            owner.Group.Add(dead);
            var dos = Burntime.Remaster.AI.AiStateOperations.GetCampDefenders(camp, attacker, new[] { owner }).ToArray();
            Equal(true, dos.SequenceEqual(new[] { guard, boss }), "DOS includes stationed guard and present owner, excludes dead members");
            Equal(true, Burntime.Remaster.AI.CombatStrength.Defenders(camp).SequenceEqual(dos),
                "Modern roster includes the owner party, not unrelated visitors");
            var amiga = Burntime.Remaster.AI.AiStateOperations.GetCampDefenders(camp, attacker, new[] { attacker, owner, visitor }).ToArray();
            Equal(true, amiga.SequenceEqual(new[] { guard, boss, visitingBoss }), "Amiga includes other opposing parties");
            owner.Traveling = true;
            Equal(true, Burntime.Remaster.AI.AiStateOperations.GetCampDefenders(camp, attacker, new[] { owner })
                .SequenceEqual(new[] { guard }), "travelling party excluded, guard remains");
            Equal(true, Burntime.Remaster.AI.CombatStrength.Defenders(camp).SequenceEqual(new[] { guard }),
                "Modern excludes parties already travelling");
            owner.Traveling = false;
            owner.Location = manager.Create(() => new Burntime.Remaster.Logic.Location());
            Equal(1, Burntime.Remaster.AI.AiStateOperations.GetCampDefenders(camp, attacker, new[] { owner }).Count(), "absent party excluded");
            return 0;
        });
    }
}
