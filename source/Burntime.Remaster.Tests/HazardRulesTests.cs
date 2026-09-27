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

static class HazardRulesTests
{
    internal static IEnumerable<Case<int>> ContinuousHazardCases()
    {
        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
            yield return Int($"{rule} inventory prioritizes hazard wearables", 0, () =>
            {
                var manager = new StateManager(null!);
                var character = manager.Create<HazardCharacter>();
                var location = manager.Create<Location>();
                character.Place(location);
                var mask = manager.Create<Item>(manager.Create(() => new HazardProtectionType("gas")));
                var suit = manager.Create<Item>(manager.Create(() => new HazardProtectionType("radiation")));
                var jacket = TestItem(manager, "jacket", defense: 20);
                var rules = new GameRules(rule);
                character.Items.Add(mask);
                rules.SelectCombatLoadout(character);
                Equal(mask, character.Protection, "only wearable auto-equips even without hazard");
                character.Items.Add(jacket);
                rules.SelectCombatLoadout(character);
                Equal(jacket, character.Protection, "safe location prefers damage protection");
                location.Danger = new("gas", 95, "", null!) { DataName = "gas" };
                rules.SelectCombatLoadout(character);
                Equal(mask, character.Protection, "gas mask takes priority over jacket");
                Equal(mask, TableCombat.PreferredProtection(character), "combat preserves hazard protection");
                character.Items.Add(suit);
                location.Danger = new("radiation", 95, "", null!) { DataName = "radiation" };
                rules.SelectCombatLoadout(character);
                Equal(suit, character.Protection, "hazard change selects matching protection");
                character.Items.Remove(suit);
                rules.SelectCombatLoadout(character);
                Equal(jacket, character.Protection, "no matching protection falls back to jacket");
                location.Danger = null;
                rules.SelectCombatLoadout(character);
                Equal(jacket, character.Protection, "leaving hazard prefers clothing");
                return 0;
            });

        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
        foreach (string hazard in new[] { "gas", "radiation" })
        {
            yield return Int($"{rule} {hazard} exposure, equipment and death", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var game = manager.Create(() => new ClassicGame());
                manager.Root = game;
                game.SetRules(rule);
                var owner = manager.Create<HazardPlayer>(new object[] { 0 });
                owner.Type = Burntime.Remaster.Logic.PlayerType.Human;
                var character = manager.Create(() => new HazardCharacter());
                character.Player = owner;
                character.Items = manager.Create<ItemList>();
                character.Health = 100;
                var location = manager.Create(() => new Burntime.Remaster.Logic.Location());
                location.Danger = new Burntime.Remaster.Logic.Interaction.Danger(hazard, 95, "", null!)
                    { DataName = "test_hazard" };
                character.Place(location);
                GameRules rules = new(rule);
                for (int frame = 0; frame < 20; frame++)
                    Equal(false, rules.ApplyContinuousHazard(character, 0.1f), "survives exposure");
                float expected = 100 - (hazard == "gas" ? 0.5f : 1.35f) * 2;
                Equal(true, Math.Abs(expected - character.ExactHealth) < 0.001f, "elapsed-time damage");
                var type = manager.Create(() => new HazardProtectionType(hazard));
                var protection = manager.Create<Item>(type);
                character.Items.Add(protection);
                character.Health = 100;
                Equal(false, rules.ApplyContinuousHazard(character, 10), "protected");
                Equal(100, character.Health, "auto-equips protection");
                Equal(true, rules.PassesDailyHazardCheck(character), "protection also passes daily check");
                character.Items.Remove(protection);
                character.Protection = null;
                character.FaceID = 10;
                rules.ApplyContinuousHazard(character, 2);
                Equal(hazard == "gas", character.Health == 100, "configured gas immunity");
                Equal(hazard == "gas", rules.PassesDailyHazardCheck(character), "daily immunity");
                Equal(1f, character.GetHazardProtectionRate("gas"), "inventory shows innate 100% gas protection without equipment");
                Equal(0f, character.GetHazardProtectionRate("radiation"), "innate immunity gives no radiation protection");
                Equal(hazard == "gas" ? 0f : 1f, character.GetDangerRate(), "danger assessment includes innate immunity");
                character.FaceID = 0;
                Equal(0f, character.GetHazardProtectionRate("gas"), "ordinary face has no innate protection");
                character.Health = 1;
                Equal(true, rules.ApplyContinuousHazard(character, 4), "lethal exposure");
                Equal(1, character.DeathCalls, "normal death handling invoked");

                character.Health = 100;
                character.DeathCalls = 0;
                owner.Type = Burntime.Remaster.Logic.PlayerType.Ai;
                Equal(false, rules.ApplyContinuousHazard(character, 20), "AI ignores on-map timer");
                Equal(100, character.Health, "AI timed immunity");
                HazardRules.ApplyDaily(character);
                Equal(1, character.DeathCalls, "stationed AI dies at daily check");
                character.Health = 100;
                character.DeathCalls = 0;
                owner.Party.Add(character);
                HazardRules.ApplyDaily(character);
                Equal(100, character.Health, "active AI party skips daily check");

                owner.Type = Burntime.Remaster.Logic.PlayerType.Human;
                owner.Traveling = true;
                HazardRules.ApplyDaily(character);
                Equal(100, character.Health, "departing human party skips daily check");
                rules.ApplyContinuousHazard(character, 2);
                Equal(true, character.Health < 100, "departing humans still take timed damage");

                owner.Traveling = false;
                character.Health = 100;
                character.Food = 9;
                character.Water = 5;
                rules.TurnEmployedCharacter(character);
                Equal(1, character.DeathCalls, "normal daily turn kills an exposed human party");
                return 0;
            });
        }
    }
}
