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
