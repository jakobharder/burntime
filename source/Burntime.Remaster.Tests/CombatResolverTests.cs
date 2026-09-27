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

static class CombatResolverTests
{
    internal static IEnumerable<Case<int>> EncounterCases()
    {
        yield return Int("defender flees only after the complete retaliation batch", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var game = manager.Create(() => new ClassicGame());
            manager.Root = game;
            game.SetRules(RuleSet.Dos);

            HazardCharacter Fighter(string id)
            {
                var fighter = manager.Create(() => new HazardCharacter());
                fighter.Class = CharClass.Mercenary;
                fighter.Health = 100;
                fighter.Items = manager.Create<ItemList>();
                fighter.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
                fighter.Items.Add(TestItem(manager, id, damage: 1,
                    damageValues: new[] { 1 }, attackRange: 16));
                return fighter;
            }

            HazardCharacter first = Fighter("first_knife");
            HazardCharacter second = Fighter("second_knife");
            HazardCharacter third = Fighter("third_knife");
            HazardCharacter fourth = Fighter("fourth_knife");
            HazardCharacter defender = Fighter("defender_knife");
            var encounter = new LocalCombatEncounter(
                new[] { first, second, third, fourth }, defender);

            encounter.Update(0);
            Equal(96, defender.Health, "all four group members attack individually");
            Equal(99, first.Health, "first retaliation resolves");
            Equal(100, second.Health, "second retaliation remains queued");
            Equal(100, third.Health, "third retaliation remains queued");
            Equal(100, fourth.Health, "fourth retaliation remains queued");
            Equal(false, defender.IsFleeing, "no flee between retaliations");

            encounter.Update(0);
            Equal(99, second.Health, "second retaliation resolves");
            Equal(false, defender.IsFleeing, "no flee before third retaliation");
            encounter.Update(0);
            Equal(99, third.Health, "third retaliation resolves");
            Equal(false, defender.IsFleeing, "no flee before fourth retaliation");
            encounter.Update(0);
            Equal(99, fourth.Health, "fourth retaliation resolves");
            Equal(true, defender.IsFleeing, "flee starts after the queue is empty");
            Equal(true, encounter.IsComplete, "encounter completes after retaliation batch");
            return 0;
        });

        yield return Int("combat approach immediately replaces the previous destination", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var attacker = manager.Create(() => new HazardCharacter());
            var defender = manager.Create(() => new HazardCharacter());
            attacker.Items = manager.Create<ItemList>();
            defender.Items = manager.Create<ItemList>();
            attacker.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
            attacker.Position = new Burntime.Platform.Vector2(100, 80);
            defender.Position = new Burntime.Platform.Vector2(20, 30);
            attacker.Path.MoveTo = new Burntime.Platform.Vector2(140, 120);
            attacker.BeginFleeFrom(defender);
            Equal(true, attacker.IsFleeing, "previous encounter starts fleeing");

            attacker.BeginCombatApproach(defender, 16);

            Equal(false, attacker.IsFleeing, "new combat interrupts previous flee");
            Equal(defender.Position, attacker.Path.MoveTo,
                "combat target replaces formation destination");
            return 0;
        });

        yield return Int("retaliation waits for the complete group attack phase", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var game = manager.Create(() => new ClassicGame());
            manager.Root = game;
            game.SetRules(RuleSet.Dos);

            HazardCharacter Fighter(string id, int range = 16)
            {
                var fighter = manager.Create(() => new HazardCharacter());
                fighter.Class = CharClass.Mercenary;
                fighter.Health = 100;
                fighter.Items = manager.Create<ItemList>();
                fighter.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
                fighter.Items.Add(TestItem(manager, id, damage: 1,
                    damageValues: new[] { 1 }, attackRange: range));
                return fighter;
            }

            HazardCharacter first = Fighter("first_knife");
            HazardCharacter distant = Fighter("distant_knife");
            HazardCharacter defender = Fighter("defender_knife");
            distant.Position = new Burntime.Platform.Vector2(100, 0);
            var encounter = new LocalCombatEncounter(new[] { first, distant }, defender);

            encounter.Update(0);
            Equal(99, defender.Health, "near group member attacks");
            Equal(100, first.Health, "retaliation waits for distant group member");
            Equal(true, first.IsHeldForCombat, "committed attacker stays available");

            encounter.CancelOffense();
            encounter.Update(0);
            Equal(99, first.Health, "retaliation begins after attack phase ends");
            return 0;
        });

        yield return Int("completed attacker can move while the group finishes", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var game = manager.Create(() => new ClassicGame());
            manager.Root = game;
            game.SetRules(RuleSet.Dos);

            HazardCharacter Fighter(string id)
            {
                var fighter = manager.Create(() => new HazardCharacter());
                fighter.Class = CharClass.Mercenary;
                fighter.Health = 100;
                fighter.Items = manager.Create<ItemList>();
                fighter.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
                fighter.Items.Add(TestItem(manager, id, damage: 1,
                    damageValues: new[] { 1 }, attackRange: 16));
                return fighter;
            }

            HazardCharacter boss = Fighter("boss_knife");
            HazardCharacter follower = Fighter("follower_knife");
            HazardCharacter defender = Fighter("defender_knife");
            follower.Position = new Burntime.Platform.Vector2(100, 0);
            var encounter = new LocalCombatEncounter(new[] { boss, follower }, defender);

            encounter.Update(0);
            Equal(99, defender.Health, "boss attacks while follower approaches");
            Equal(true, encounter.ReleaseAfterCompletedAttack(boss),
                "completed boss may leave combat hold");
            Equal(false, boss.IsCommittedToCombat, "boss movement is released");

            follower.Position = defender.Position;
            encounter.Update(0);
            Equal(98, defender.Health, "follower attack remains active");
            Equal(99, boss.Health, "boss still receives queued retaliation");
            return 0;
        });

        yield return Int("new encounter stops the defender's previous flee", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var game = manager.Create(() => new ClassicGame());
            manager.Root = game;
            game.SetRules(RuleSet.Dos);
            HazardCharacter attacker = manager.Create(() => new HazardCharacter());
            HazardCharacter defender = manager.Create(() => new HazardCharacter());
            attacker.Health = defender.Health = 100;
            attacker.Items = manager.Create<ItemList>();
            defender.Items = manager.Create<ItemList>();
            attacker.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
            defender.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
            attacker.BeginFleeFrom(defender);
            defender.BeginFleeFrom(attacker);

            _ = new LocalCombatEncounter(new[] { attacker }, defender);

            Equal(false, attacker.IsFleeing, "attacker stops fleeing to approach");
            Equal(true, attacker.IsCommittedToCombat,
                "accepted attack owns movement while a direction remains held");
            Equal(false, defender.IsFleeing, "defender stops fleeing for new round");
            Equal(true, defender.IsHeldForCombat, "defender waits for group attack");
            return 0;
        });

        yield return Int("blocked retaliation still resolves exactly once", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var game = manager.Create(() => new ClassicGame());
            manager.Root = game;
            game.SetRules(RuleSet.Dos);
            HazardCharacter attacker = manager.Create(() => new HazardCharacter());
            HazardCharacter defender = manager.Create(() => new HazardCharacter());
            attacker.Class = defender.Class = CharClass.Mercenary;
            attacker.Health = defender.Health = 100;
            attacker.Items = manager.Create<ItemList>();
            defender.Items = manager.Create<ItemList>();
            attacker.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
            defender.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
            attacker.Items.Add(TestItem(manager, "attacker_rifle", damage: 1,
                damageValues: new[] { 1 }, attackRange: 32));
            defender.Items.Add(TestItem(manager, "defender_knife", damage: 1,
                damageValues: new[] { 1 }, attackRange: 16));
            attacker.Position = new Burntime.Platform.Vector2(32, 0);
            var encounter = new LocalCombatEncounter(new[] { attacker }, defender);

            encounter.Update(0);
            Equal(99, defender.Health, "ranged attack resolves");
            Equal(100, attacker.Health, "retaliation initially approaches");
            encounter.Update(2);
            Equal(99, attacker.Health, "timed-out path cannot discard retaliation");
            Equal(true, encounter.IsComplete, "resolved encounter releases input");
            return 0;
        });

        yield return Int("weapon attack range overrides the Amiga unarmed fallback", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var fighter = manager.Create(() => new HazardCharacter());
            fighter.Class = CharClass.Mercenary;
            fighter.Items = manager.Create<ItemList>();
            Equal(16f, fighter.AttackRange, "unarmed range");
            fighter.Items.Add(TestItem(manager, "item_loaded_rifle", damage: 1,
                damageValues: new[] { 1 }, ammo: 6, attackRange: 32));
            Equal(32f, fighter.AttackRange, "configured weapon range");
            return 0;
        });

        yield return Int("longer reach defender intercepts before a shorter attacker", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var game = manager.Create(() => new ClassicGame());
            manager.Root = game;
            game.SetRules(RuleSet.Dos);

            HazardCharacter Fighter(string id, int range, int health)
            {
                var fighter = manager.Create(() => new HazardCharacter());
                fighter.Class = CharClass.Mercenary;
                fighter.Health = health;
                fighter.Items = manager.Create<ItemList>();
                fighter.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
                fighter.Items.Add(TestItem(manager, id, damage: 1,
                    damageValues: new[] { 1 }, attackRange: range));
                return fighter;
            }

            HazardCharacter attacker = Fighter("attacker_knife", 15, 1);
            HazardCharacter defender = Fighter("defender_pitchfork", 28, 100);
            var encounter = new LocalCombatEncounter(new[] { attacker }, defender);

            encounter.Update(0);
            Equal(true, attacker.IsDead, "longer reach lands the first strike");
            Equal(100, defender.Health, "dead attacker cannot answer the intercept");
            Equal(true, encounter.IsComplete, "intercepted exchange completes");
            return 0;
        });

        yield return Int("reach intercept replaces rather than adds retaliation", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var game = manager.Create(() => new ClassicGame());
            manager.Root = game;
            game.SetRules(RuleSet.Dos);

            HazardCharacter Fighter(string id, int range)
            {
                var fighter = manager.Create(() => new HazardCharacter());
                fighter.Class = CharClass.Mercenary;
                fighter.Health = 100;
                fighter.Items = manager.Create<ItemList>();
                fighter.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
                fighter.Items.Add(TestItem(manager, id, damage: 1,
                    damageValues: new[] { 1 }, attackRange: range));
                return fighter;
            }

            HazardCharacter attacker = Fighter("attacker_knife", 15);
            HazardCharacter defender = Fighter("defender_pitchfork", 28);
            var encounter = new LocalCombatEncounter(new[] { attacker }, defender);

            encounter.Update(0);
            Equal(99, attacker.Health, "defender intercepts exactly once");
            Equal(99, defender.Health, "surviving attacker answers the intercept");
            Equal(true, encounter.IsComplete, "no second retaliation is queued");
            return 0;
        });

        yield return Int("same reach tier preserves attacker initiative", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var game = manager.Create(() => new ClassicGame());
            manager.Root = game;
            game.SetRules(RuleSet.Dos);

            HazardCharacter Fighter(string id, int range, int damage, int health)
            {
                var fighter = manager.Create(() => new HazardCharacter());
                fighter.Class = CharClass.Mercenary;
                fighter.Health = health;
                fighter.Items = manager.Create<ItemList>();
                fighter.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
                fighter.Items.Add(TestItem(manager, id, damage: damage,
                    damageValues: new[] { damage }, attackRange: range));
                return fighter;
            }

            HazardCharacter attacker = Fighter("attacker_knife", 15, 1, 100);
            HazardCharacter defender = Fighter("defender_close", 16, 100, 1);
            var encounter = new LocalCombatEncounter(new[] { attacker }, defender);

            encounter.Update(0);
            Equal(true, defender.IsDead, "initiator wins a tied reach tier");
            Equal(100, attacker.Health, "dead defender cannot retaliate");
            return 0;
        });
    }

    internal static IEnumerable<Case<int>> LocalCombatCases()
    {
        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
        foreach (bool lethal in new[] { false, true })
            yield return Int($"{rule} local retaliation, lethal={lethal}", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var game = manager.Create(() => new ClassicGame());
                manager.Root = game;
                game.SetRules(rule);
                var attacker = manager.Create(() => new HazardCharacter());
                var defender = manager.Create(() => new HazardCharacter());
                attacker.Class = defender.Class = CharClass.Dog;
                attacker.Items = manager.Create<ItemList>();
                defender.Items = manager.Create<ItemList>();
                attacker.Health = 100;
                defender.Health = lethal ? 1 : 100;
                attacker.Items.Add(TestItem(manager, "item_knife", damage: 7, damageValues: new[] { 7 }));
                var rifle = TestItem(manager, "item_loaded_rifle", damage: 9,
                    damageValues: new[] { 9 }, ammo: 6);
                defender.Items.Add(rifle);
                attacker.Attack(defender);
                Equal(lethal, attacker.Health == 100, "only living defenders retaliate");
                Equal(6, rifle.AmmoValue, "creature retaliation ignores inventory weapons");
                Equal(lethal ? 1 : 0, defender.DeathCalls, "lethal hit invokes death once");
                if (lethal)
                {
                    attacker.Attack(defender);
                    Equal(1, defender.DeathCalls, "dead targets cannot be attacked again");
                }
                return 0;
            });
    }
}
