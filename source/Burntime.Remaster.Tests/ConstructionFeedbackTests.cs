using System.Collections.Generic;
using System.Linq;
using Burntime.Framework.States;
using Burntime.Platform.IO;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Interaction;

namespace Burntime.Remaster.Tests;

using static Program;

static class ConstructionFeedbackTests
{
    internal static IEnumerable<Case<int>> AvailabilityCases()
    {
        yield return Int("metal detector recipe uses repair electronics without LCD", 0, () =>
        {
            ConfigFile config = new();
            config.Open(System.IO.File.OpenRead(ResourceFile("construction.txt")));
            Constructions constructions = new(config);
            var recipe = constructions.Recipes.Single(candidate =>
                candidate.Result == "item_mine_detector");

            Equal(ItemFunction.MetalDetection, recipe.RequiredFunction,
                "extended function gate");
            Equal("item_defective_mine_detector item_electrical_odds_and_ends item_batteries",
                string.Join(' ', recipe.Items), "materials");
            Equal(false, recipe.Items.Contains("item_lcd_display"), "LCD omitted");
            return 0;
        });

        yield return Int("reload reports missing ammunition and becomes available", 0, () =>
        {
            ConfigFile config = new();
            config.Open(System.IO.File.OpenRead(ResourceFile("construction.txt")));
            Constructions constructions = new(config);
            StateManager manager = new(null!);
            HazardCharacter character = manager.Create(() => new HazardCharacter());
            character.Class = CharClass.Mercenary;
            character.Items = manager.Create<ItemList>();
            Item rifle = TestItem(manager, "item_unloaded_rifle");
            character.Items.Add(rifle);
            ItemList room = manager.Create<ItemList>();

            var blocked = constructions.EvaluateConstruction(character, room, rifle);
            Equal("item_loaded_rifle", blocked.Recipe?.Result,
                "reload recipe");
            Equal(false, blocked.CanBuild, "reload initially blocked");
            Equal(1, blocked.MissingRequirements.Count,
                "one missing requirement");
            Equal("item_ammunition", blocked.MissingRequirements[0],
                "ammunition identified");

            room.Add(TestItem(manager, "item_ammunition"));
            var available = constructions.EvaluateConstruction(character, room, rifle);
            Equal(true, available.CanBuild, "reload available with ammunition");
            Equal(0, available.MissingRequirements.Count,
                "no missing requirements");
            return 0;
        });

        yield return Int("nearby technician inspects another group member's materials", 0, () =>
        {
            ConfigFile config = new();
            config.Open(System.IO.File.OpenRead(ResourceFile("construction.txt")));
            Constructions constructions = new(config);
            StateManager manager = new(null!);
            ClassicGame game = manager.Create(() => new ClassicGame());
            manager.Root = game;
            game.Constructions = constructions;

            HazardCharacter boss = manager.Create(() => new HazardCharacter());
            boss.Class = CharClass.Boss;
            boss.Items = manager.Create<ItemList>();
            HazardCharacter technician = manager.Create(() => new HazardCharacter());
            technician.Class = CharClass.Technician;
            technician.Items = manager.Create<ItemList>();
            Item brokenPump = TestItem(manager, "item_broken_pump");
            boss.Items.Add(brokenPump);
            boss.Items.Add(TestItem(manager, "item_rags"));
            boss.Items.Add(TestItem(manager, "item_hose"));
            ItemList room = manager.Create<ItemList>();

            Character inspector = constructions.SelectInspector(game, boss,
                new Character[] { boss, technician }, brokenPump);
            Equal(technician, inspector, "technician selected without changing item owner");
            var availability = constructions.EvaluateConstruction(inspector,
                boss.Items, room, brokenPump);
            Equal("item_hand_pump", availability.Recipe?.Result, "technician recipe");
            Equal(true, availability.CanBuild, "owner inventory supplies the materials");
            return 0;
        });
    }
}
