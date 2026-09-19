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

static class GameRulesTests
{
    internal static IEnumerable<Case<int>> ConfiguredRuleCases()
    {
        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
            yield return Int($"{rule} configured doctor, barter and damage", 0, () =>
            {
                var rules = new GameRules(rule);
                Equal(37, rules.Settings.StartExperience, "shared boss starting XP");
                if (rule is RuleSet.Amiga or RuleSet.Classic)
                {
                    int[][] regions = { new[] { 26, 34, 19, 20 }, new[] { 31, 28, 33, 23 },
                        new[] { 11, 12, 17, 10 }, new[] { 5, 3, 9, 2 } };
                    for (int difficulty = 0; difficulty < 3; difficulty++)
                    {
                        rules.Settings.SetDifficulty(difficulty);
                        Equal(4, rules.Settings.StartRegionCount, "Amiga regional placement group count");
                        for (int region = 1; region <= 4; region++)
                            Equal(true, regions[region - 1].SequenceEqual(rules.Settings.GetStartLocation(region)),
                                "Amiga regional placement preserves original locations");
                    }
                }

                var config = new Burntime.Platform.IO.ConfigFile();
                config.Open(System.IO.File.OpenRead(ResourceFile(
                    new Burntime.Platform.Resource.ResourceID(GameDefinitions.Get(rule).ItemsPath).File)));
                var manager = new Burntime.Framework.States.StateManager(null!);
                var payment = manager.Create<ItemList>();
                var meat = TestItem(manager, "item_meat", food: 9,
                    heal: config["item_meat"].GetInt("heal"), trade: 27);
                payment.Add(meat);
                Equal(rule == RuleSet.Amiga ? 37 : 46,
                    rules.CalculateDoctorResult(10, payment), "meat healing from item and settings");
                Equal(95,
                    rules.CalculateDoctorResult(90, payment), "configured health cap");
                Equal(rule switch
                    {
                        RuleSet.Dos => 80,
                        RuleSet.Classic => 90,
                        RuleSet.Extended => 90,
                        _ => 95,
                    },
                    rules.Settings.GetBarterFactor(1), "normal barter factor");
                Equal(25,
                    rules.Settings.CombatTierWidth, "combat XP tier width");
                Equal(true, rules.Settings.FightClasses.Contains("fighter"),
                    "fighters use full combat XP");
                Equal(rule != RuleSet.Amiga, rules.Settings.FightClasses.Contains("trader"),
                    "trader combat XP follows the ruleset");
                var demand = manager.Create<ItemList>();
                demand.Add(TestItem(manager, "payment", trade: 27));
                Equal(false, rules.AcceptTrade(payment, demand, 1),
                    "normal trade uses the configured factor");
                Equal(100, rules.Settings.GetBarterFactor(0), "easy barter factor");
                Equal(rule switch
                    {
                        RuleSet.Dos => 60,
                        RuleSet.Classic => 80,
                        RuleSet.Extended => 80,
                        _ => 90,
                    },
                    rules.Settings.GetBarterFactor(2), "hard barter factor");
                rules.Settings.SetDifficulty(0);
                Equal(rule == RuleSet.Dos ? RespawnMethod.PlayerCycle : RespawnMethod.LocationCycle,
                    rules.Settings.Respawn.Method, "configured NPC spawn method");
                Equal(rule switch
                    {
                        RuleSet.Amiga => 8,
                        RuleSet.Classic => 8,
                        RuleSet.Dos => 4,
                        _ => 4,
                    },
                    rules.Settings.Respawn.NPC, "configured easy NPC spawn interval");
                Equal(rule == RuleSet.Extended ? 12 : 0,
                    rules.Settings.Respawn.CitySpawnThreshold,
                    "configured city spawn threshold");
                Equal(rule == RuleSet.Extended ? 10 : 0,
                    rules.Settings.DroppedFoodDecayInterval,
                    "dropped food decay is enabled only in extended mode");
                var decayLocation = manager.Create<Location>();
                decayLocation.Id = 2;
                var junk = TestItem(manager, "junk");
                var firstFood = TestItem(manager, "food-a", food: 3);
                var secondFood = TestItem(manager, "food-b", food: 6);
                decayLocation.Items.Add(junk);
                decayLocation.Items.Add(firstFood);
                decayLocation.Items.Add(secondFood);
                GameRules.ProcessDroppedFoodDecay(decayLocation, day: 7, interval: 10);
                Equal(3, decayLocation.Items.Count, "food remains before its location cycle");
                GameRules.ProcessDroppedFoodDecay(decayLocation, day: 8, interval: 10);
                Equal(2, decayLocation.Items.Count, "one food decays on its location cycle");
                Equal(true, decayLocation.Items.Any(item => item == junk),
                    "non-food ground items do not decay");
                Equal(true, decayLocation.Items.Any(item => item == secondFood),
                    "only one ground food decays per cycle");
                Equal(true, rules.AcceptTrade(payment, demand, 0), "equal offer accepted on easy");
                Equal(false, rules.AcceptTrade(payment, demand, 2), "equal offer rejected on hard");
                Equal(16,
                    config["item_knife"].GetInts("damage").Length, "damage format");
                Equal(true, RuleFormulas.OriginalDamage(new[] { 7 }, 99, 25, 3) == 7,
                    "scalar damage supports every tier and roll");
                int[] traderDamage = rule is RuleSet.Amiga or RuleSet.Classic
                    ? new[] { 9, 11, 13, 15 }
                    : new[] { 8, 16, 25, 40 };
                Equal(true, traderDamage.SequenceEqual(rules.Settings.GetTraderAttack(0)),
                    "configured trader damage rolls");
                Equal(traderDamage[3],
                    RuleFormulas.OriginalDamage(traderDamage, 99, 25, 3),
                    "trader damage is a fixed four-roll table");
                for (int difficulty = 0; difficulty < 3; difficulty++)
                {
                    int bonus = rule == RuleSet.Extended ? difficulty * 2 : 0;
                    int[] creatureDamage = rule == RuleSet.Dos
                        ? new[] { 2, 2, 2, 2 }
                        : new[] { 2 + bonus, 3 + bonus, 4 + bonus, 6 + bonus };
                    Equal(true, creatureDamage.SequenceEqual(
                        rules.Settings.GetMutantAttack(difficulty)),
                        "configured mutant damage rolls");
                    Equal(true, creatureDamage.SequenceEqual(
                        rules.Settings.GetDogAttack(difficulty)),
                        "configured dog damage rolls");
                }

                if (rule != RuleSet.Dos)
                {
                    int interval = rules.Settings.Respawn.NPC;
                    for (int location = 0; location < 3; location++)
                    {
                        int dueDays = Enumerable.Range(1, interval * 2)
                            .Count(day => LocationCycle.IsDue(day, location, interval));
                        Equal(2, dueDays, "location cycle runs once per interval");
                    }
                    Equal(false,
                        LocationCycle.IsDue(1, 0, 0),
                        "disabled location cycle never runs");
                }
                return 0;
            });
    }
}
