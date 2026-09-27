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

static class OriginalRulesRegressionTests
{
    internal static IEnumerable<Case<int>> OriginalRegressionCases()
    {
        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
        {
            yield return Int($"{rule} production comes from its config", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var food = TestItem(manager, "item_snake", food: 7).Type;
                var config = new Burntime.Platform.IO.ConfigFile();
                config.Open(System.IO.File.OpenRead(ResourceFile(GameDefinitions.Get(rule).ProductionPath)));
                var section = config["item_snake_trap"];
                var snakes = manager.Create(() => new Burntime.Remaster.Logic.Production(
                    section.GetInt("maxcombination"), section.GetInts("amount"),
                    section.GetInts("amount2"), food, 2));
                Equal(5, snakes.GetRate(2, 1).FoodPerDay, "two traps, one employee");
                Equal(rule == RuleSet.Extended ? section.GetInts("amount")[1] :
                    rule is RuleSet.Amiga or RuleSet.Classic ? 4 : 3,
                    snakes.GetRate(1, 1).FoodPerDay, "single snake trap");
                Equal(5, snakes.GetRate(2, 0).FoodPerDay, "no rules-specific staffing check");
                snakes.ApplySettings(2, new[] { 0, 8, 9 }, Array.Empty<int>());
                Equal(8, snakes.GetRate(1, 1).FoodPerDay, "no hardcoded edition override");
                return 0;
            });
        }
        yield return Int("profile item paths exist", 0, () =>
        {
            foreach (RuleSet rule in Enum.GetValues<RuleSet>())
            {
                var path = new Burntime.Platform.Resource.ResourceID(GameDefinitions.Get(rule).ItemsPath).File;
                Equal(true, System.IO.File.Exists(ResourceFile(path)), "item file exists");
            }
            return 0;
        });
        yield return Int("DOS base zero industrial minimum boost", 2,
            () => new GameRules(RuleSet.Dos).CalculateWaterOutput(0, false, true));
        yield return Int("DOS base 3 industrial", 5,
            () => new GameRules(RuleSet.Dos).CalculateWaterOutput(3, false, true));
        yield return Int("Amiga base 3 industrial", 6,
            () => new GameRules(RuleSet.Amiga).CalculateWaterOutput(3, false, true));
        yield return Int("Classic uses useful original pump minimum", 6,
            () => new GameRules(RuleSet.Classic).CalculateWaterOutput(3, false, true));
        yield return Int("DOS base 1 industrial precedence", 3,
            () => new GameRules(RuleSet.Dos).CalculateWaterOutput(1, true, true));
        yield return Int("Amiga stock removal residue classes", 0, () =>
        {
            for (int day = 0; day < 8; day++)
                for (int residue = 0; residue < 8; residue++)
                    Equal(residue == day || residue == (day + 5) % 8,
                        RuleFormulas.AmigaTraderRemovalCandidate(0x40 + residue, day),
                        $"day {day}, residue {residue}");
            Equal(true, RuleFormulas.AmigaTraderRemovalCandidate(0x36, 1), "food cleared");
            return 0;
        });
        yield return Int("unloaded rifle retained only for Amiga strategic combat", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var fighter = manager.Create(() => new Burntime.Remaster.Logic.Character());
            fighter.Items = manager.Create<ItemList>();
            Item rifle = TestItem(manager, "item_unloaded_rifle");
            fighter.Items.Add(rifle);
            Equal(rifle, fighter.SelectOriginalWeapon(allowUnloadedRifle: true), "Amiga rifle");
            Equal<Item?>(null, fighter.SelectOriginalWeapon(), "on-map unarmed");
            Item knife = TestItem(manager, "item_knife", damage: 25);
            fighter.Items.Add(knife);
            Equal(knife, fighter.SelectOriginalWeapon(allowUnloadedRifle: true), "prefer knife");
            return 0;
        });
        yield return Int("food cap follows the selected product across changes", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var camp = manager.Create(() => new Burntime.Remaster.Logic.Location());
            camp.Rooms = manager.CreateLinkList<Burntime.Remaster.Logic.Room>();
            var room = manager.Create(() => new Burntime.Remaster.Logic.Room());
            room.Items = manager.Create<ItemList>();
            camp.Rooms.Add(room);
            for (int n = 0; n < 3; n++)
            {
                room.Items.Add(TestItem(manager, "item_maggots", food: 3));
                room.Items.Add(TestItem(manager, "item_meat", food: 9));
            }
            room.Items.Add(TestItem(manager, "item_knife", damage: 25));
            Equal(0, camp.GetCurrentProductionStockCount(), "no selected product");
            var foodType = room.Items[0].Type;
            camp.Production = manager.Create(() => new Burntime.Remaster.Logic.Production(
                1, new[] { 1, 2 }, new[] { 1, 3 }, foodType, 0));
            Equal(3, camp.GetCurrentProductionStockCount(), "meat does not count against maggots");
            for (int n = 0; n < 3; n++)
                room.Items.Add(TestItem(manager, "item_maggots", food: 3));
            Equal(6, camp.GetCurrentProductionStockCount(), "six maggots fill the cap");
            var meatType = room.Items[1].Type;
            camp.Production = manager.Create(() => new Burntime.Remaster.Logic.Production(
                2, new[] { 0, 5, 7 }, Array.Empty<int>(), meatType, 3));
            Equal(3, camp.GetCurrentProductionStockCount(), "maggots do not count against meat");
            return 0;
        });
    }
}
