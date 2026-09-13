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

static class TableCombatTests
{
    static int[] ReadDamage(string? weapon, RuleSet rule = RuleSet.Dos)
    {
        var config = new Burntime.Platform.IO.ConfigFile();
        config.Open(System.IO.File.OpenRead(ResourceFile(new Burntime.Platform.Resource.ResourceID(GameDefinitions.Get(rule).ItemsPath).File)));
        return config[weapon ?? ""].GetInts("damage");
    }

    internal static IEnumerable<Case<int>> OriginalCombatCases()
    {
        (string? Weapon, int[][] Tiers)[] tables =
        {
            (null, new[]
            {
                new[] { 2, 3, 4, 6 }, new[] { 6, 8, 10, 11 },
                new[] { 8, 10, 12, 15 }, new[] { 9, 11, 13, 15 }
            }),
            ("item_knife", new[]
            {
                new[] { 9, 11, 14, 16 }, new[] { 10, 13, 16, 18 },
                new[] { 12, 14, 17, 20 }, new[] { 14, 16, 18, 20 }
            }),
            ("item_axe", new[]
            {
                new[] { 9, 11, 15, 20 }, new[] { 9, 12, 17, 22 },
                new[] { 10, 14, 19, 25 }, new[] { 13, 17, 21, 25 }
            }),
            ("item_pitchfork", new[]
            {
                new[] { 4, 7, 10, 20 }, new[] { 5, 8, 11, 21 },
                new[] { 6, 9, 12, 25 }, new[] { 9, 12, 16, 25 }
            }),
            ("item_loaded_rifle", new[]
            {
                new[] { 0, 6, 14, 15 }, new[] { 0, 10, 17, 20 },
                new[] { 5, 13, 19, 25 }, new[] { 8, 16, 25, 40 }
            })
        };

        foreach ((string? weapon, int[][] tiers) in tables)
        foreach (int tierWidth in new[] { 25, 26 })
        for (int tier = 0; tier < tiers.Length; tier++)
        for (int roll = 0; roll < tiers[tier].Length; roll++)
        {
            string weaponName = weapon ?? "unarmed";
            int capturedTier = tier;
            int capturedRoll = roll;
            yield return Int(
                $"{weaponName}, width {tierWidth}, tier {tier}, roll {roll}",
                tiers[tier][roll],
                () => RuleFormulas.OriginalDamage(
                    ReadDamage(weapon, tierWidth == 25 ? RuleSet.Dos : RuleSet.Amiga), capturedTier * tierWidth, tierWidth, capturedRoll));
        }

        yield return Int("DOS tier clamps above 99", 20,
            () => RuleFormulas.OriginalDamage(ReadDamage("item_knife"), 500, 25, 3));
        yield return Int("unknown weapon is unarmed", 2,
            () => RuleFormulas.OriginalDamage(ReadDamage(null), 0, 25, 0));
    }

    internal static IEnumerable<Case<int>> CombatPreviewCases()
    {
        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
            yield return Int($"{rule} trader uses configured attack instead of stock", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var trader = manager.Create(() => new Burntime.Remaster.Logic.Character());
                trader.Class = CharClass.Trader;
                trader.Experience = 99;
                trader.Items = manager.Create<ItemList>();
                var rifle = TestItem(manager, "item_loaded_rifle", damage: 55, ammo: 6,
                    damageValues: ReadDamage("item_loaded_rifle", rule));
                trader.Items.Add(rifle);
                trader.Weapon = rifle;
                var defender = manager.Create(() => new Burntime.Remaster.Logic.Character());
                defender.Items = manager.Create<ItemList>();
                defender.Health = 100;
                var rules = new GameRules(rule);
                var preview = rules.GetCombatPreview(trader);
                Equal(rule is RuleSet.Amiga or RuleSet.Classic ? 9 : 8, preview.Minimum,
                    "configured trader minimum");
                Equal(rule is RuleSet.Amiga or RuleSet.Classic ? 15 : 40, preview.Maximum,
                    "configured trader maximum");
                rules.DealAttackDamage(trader, defender, true);
                Equal(6, rifle.AmmoValue, "trader stock is not used as a weapon");
                Equal(rifle, trader.Weapon, "trader weapon selection is unchanged");
                return 0;
            });

        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
        foreach (CharClass creatureClass in new[] { CharClass.Mutant, CharClass.Dog })
            yield return Int($"{rule} {creatureClass} uses configured attack", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var creature = manager.Create(() => new Burntime.Remaster.Logic.Character());
                creature.Class = creatureClass;
                creature.Experience = 99;
                creature.Items = manager.Create<ItemList>();
                var rifle = TestItem(manager, "item_loaded_rifle", damage: 55, ammo: 6,
                    damageValues: ReadDamage("item_loaded_rifle", rule));
                creature.Items.Add(rifle);
                creature.Weapon = rifle;
                var defender = manager.Create(() => new Burntime.Remaster.Logic.Character());
                defender.Items = manager.Create<ItemList>();
                defender.Health = 100;
                var preview = new GameRules(rule).GetCombatPreview(creature);
                Equal(rule == RuleSet.Extended ? 4 : 2, preview.Minimum,
                    "configured creature minimum at normal difficulty");
                Equal(rule switch
                    {
                        RuleSet.Dos => 2,
                        RuleSet.Amiga => 6,
                        RuleSet.Classic => 6,
                        _ => 8,
                    }, preview.Maximum, "configured creature maximum at normal difficulty");
                new GameRules(rule).DealAttackDamage(creature, defender, true);
                Equal(6, rifle.AmmoValue, "creature inventory is not used as a weapon");
                Equal(rifle, creature.Weapon, "creature weapon selection is unchanged");
                return 0;
            });

        foreach (RuleSet rule in new[] { RuleSet.Dos, RuleSet.Amiga })
            yield return Int($"{rule} preview follows weapon and XP without changing equipment", 0, () =>
            {
                var manager = new Burntime.Framework.States.StateManager(null!);
                var fighter = manager.Create(() => new Burntime.Remaster.Logic.Character());
                fighter.Class = CharClass.Boss;
                fighter.Experience = 50;
                fighter.Items = manager.Create<ItemList>();
                var knife = TestItem(manager, "item_knife", damage: 25,
                    damageValues: ReadDamage("item_knife", rule));
                fighter.Items.Add(knife);
                var rules = new GameRules(rule);
                var preview = rules.GetCombatPreview(fighter);
                Equal(10, preview.Minimum, "boss combat uses half XP");
                Equal(18, preview.Maximum, "weapon range");
                Equal<int?>(null, preview.Defense, "original combat has no defense rating");
                Equal<Item?>(null, fighter.Weapon, "preview does not equip a weapon");
                var rifle = TestItem(manager, "item_loaded_rifle", damage: 55, ammo: 6,
                    damageValues: ReadDamage("item_loaded_rifle", rule));
                fighter.Items.Add(rifle);
                fighter.Weapon = rifle;
                fighter.Experience = 0;
                preview = rules.GetCombatPreview(fighter);
                Equal(0, preview.Minimum, "rifle can roll zero");
                Equal(15, preview.Maximum, "selected rifle range");
                Equal(6, rifle.AmmoValue, "preview does not consume ammunition");
                Equal(rifle, fighter.Weapon, "preview preserves selected weapon");
                var unloaded = TestItem(manager, "item_unloaded_rifle");
                fighter.Items.Remove(rifle);
                fighter.Items.Add(unloaded);
                fighter.Weapon = unloaded;
                preview = rules.GetCombatPreview(fighter);
                Equal(9, preview.Minimum, "unloaded rifle falls back to knife");
                Equal(16, preview.Maximum, "fallback knife range");
                Equal(unloaded, fighter.Weapon, "fallback preview does not change selection");
                return 0;
            });
        yield return Int("unarmed and scalar previews", 0, () =>
        {
            Equal(new CombatPreview(2, 6, null),
                RuleFormulas.OriginalCombatPreview(ReadDamage(null), 0, 25), "unarmed table");
            Equal(new CombatPreview(7, 7, null),
                RuleFormulas.OriginalCombatPreview(new[] { 7 }, 99, 25), "single damage value");
            return 0;
        });
        yield return Int("Extended damage preview and percentage armour", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var fighter = manager.Create(() => new Burntime.Remaster.Logic.Character());
            fighter.Items = manager.Create<ItemList>();
            fighter.Class = CharClass.Mercenary;
            fighter.Experience = 50;
            fighter.Items.Add(TestItem(manager, "item_knife", damage: 25,
                damageValues: ReadDamage("item_knife", RuleSet.Extended)));
            var rules = new GameRules(RuleSet.Extended);
            var preview = rules.GetCombatPreview(fighter);
            Equal(new CombatPreview(12, 20, null), preview, "damage range; zero defence hidden");
            var sweater = TestItem(manager, "sweater", defense: 5);
            var jacket = TestItem(manager, "jacket", defense: 20);
            fighter.Items.Add(sweater);
            fighter.Items.Add(jacket);
            fighter.Protection = sweater;
            preview = rules.GetCombatPreview(fighter);
            Equal<int?>(20, preview.Defense, "strongest clothing, without stacking");
            Equal(sweater, fighter.Protection, "preview does not change equipment");
            fighter.Experience = 0;
            Equal<int?>(20, rules.GetCombatPreview(fighter).Defense, "armour independent of XP");
            return 0;
        });
        yield return Int("Extended local and off-screen damage share rolls and armour", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var attacker = manager.Create(() => new Burntime.Remaster.Logic.Character());
            attacker.Items = manager.Create<ItemList>();
            attacker.Class = CharClass.Mercenary;
            attacker.Experience = 50;
            var rifle = TestItem(manager, "item_loaded_rifle", damage: 55, ammo: 6,
                damageValues: ReadDamage("item_loaded_rifle", RuleSet.Extended));
            attacker.Items.Add(rifle);
            var defender = manager.Create(() => new Burntime.Remaster.Logic.Character());
            defender.Items = manager.Create<ItemList>();
            defender.Class = CharClass.Boss;
            defender.Experience = 99;
            defender.Health = 100;
            defender.Items.Add(TestItem(manager, "jacket", defense: 20));
            var rules = new GameRules(RuleSet.Extended);
            Burntime.Platform.Math.SetRandomSeed(42);
            rules.DealAttackDamage(attacker, defender, true);
            int local = 100 - defender.Health;
            Equal(5, rifle.AmmoValue, "local shot consumes ammunition");
            Burntime.Platform.Math.SetRandomSeed(42);
            int strategic = rules.RollStrategicDamage(null!, null!, attacker, defender);
            Equal(local, strategic, "same seeded local and strategic damage");
            Equal(4, rifle.AmmoValue, "off-screen shot consumes ammunition");
            defender.Experience = 0;
            Burntime.Platform.Math.SetRandomSeed(42);
            Equal(local, rules.RollStrategicDamage(null!, null!, attacker, defender), "defender XP grants no armour");
            return 0;
        });
    }

    internal static IEnumerable<Case<float>> StrategicCombatCases()
    {
        yield return Float("original detailed strength", 19.25f,
            () => RuleFormulas.OriginalStrategicStrength(
                ReadDamage("item_knife"), 25, 25, 50, detailed: true));
        yield return Float("original coarse strength", 24.25f,
            () => RuleFormulas.OriginalStrategicStrength(
                ReadDamage("item_knife"), 25, 25, 50, detailed: false));
    }

    internal static IEnumerable<Case<int>> ExtendedStrategicDamageCases()
    {
        foreach (var (raw, expected) in new[] { (12, 10), (14, 11), (17, 14), (20, 16) })
            yield return Int($"20 percent armour against {raw}", expected,
                () => RuleFormulas.ApplyArmour(raw, 20));
        yield return Int("no armour", 20, () => RuleFormulas.ApplyArmour(20, 0));
        yield return Int("rifle miss stays zero", 0, () => RuleFormulas.ApplyArmour(0, 25));
        yield return Int("weak hit remains meaningful", 2, () => RuleFormulas.ApplyArmour(2, 25));
        yield return Int("successful hits retain minimum one", 1, () => RuleFormulas.ApplyArmour(1, 25));
    }
}
