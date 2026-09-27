using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Framework.States;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.Tests;

using static Program;

static class OriginalStockTests
{
    static Item StockItem(StateManager manager, int title, int _) =>
        TestItem(manager, "title_" + title);
    static int StockTitle(ItemType type) => int.Parse(type.ID.Substring(6));

    internal static IEnumerable<Case<int>> OriginalStockCases()
    {
        foreach (int difficulty in new[] { 0, 1, 2 })
            yield return Int($"DOS cleanup difficulty {difficulty}, local collection order", 0, () =>
            {
                var manager = new StateManager(null!);
                var camp = manager.Create<Location>();
                camp.Rooms = manager.CreateLinkList<Room>();
                var room = manager.Create<Room>(); camp.Rooms.Add(room);
                Item roomLoot = StockItem(manager, 0x48, 1);
                Item groundLoot = StockItem(manager, 0x37, 3);
                Item protectedPump = StockItem(manager, 0x47, 2);
                room.Items.Add(roomLoot); room.Items.Add(protectedPump);
                camp.Items.Add(groundLoot);
                camp.Items.Add(StockItem(manager, 0x48, 4));
                OriginalItemRecords.CleanupDos(camp, difficulty, i => StockTitle(i.Type));
                Equal(difficulty == 0 ? 4 : 3, camp.Items.Count + room.Items.Count, "at most one release");
                Equal(true, room.Items.Contains(roomLoot), "ground items are checked before rooms");
                Equal(difficulty == 0, camp.Items.Any(i => i == groundLoot), "first local ground candidate removed");
                Equal(true, room.Items.Contains(protectedPump), "pump preserved in room");
                return 0;
            });

        yield return Int("DOS actual turn deletes once, not twice", 0, () =>
        {
            var m = new StateManager(null!); var game = m.Create<ClassicGame>(); m.Root = game;
            game.World = m.Create(() =>
            {
                var world = (ClassicWorld)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClassicWorld));
                foreach (string field in new[] { "ID", "localID" })
                    typeof(StateObject).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(world, -1);
                return world;
            });
            game.World.AllCharacters = m.CreateLinkList<Character>();
            game.World.Locations = m.CreateLinkList<Location>();
            var camp = m.Create<Location>(); camp.IsCity = true;
            camp.Rooms = m.CreateLinkList<Room>();
            game.World.Locations.Add(camp);
            var player = m.Create<HazardPlayer>(new object[] { 0 }); player.Location = camp;
            var boss = m.Create<HazardCharacter>(); boss.Items = m.Create<ItemList>();
            boss.Player = player; boss.Health = 100; player.Character = boss;
            game.World.AllCharacters.Add(boss);
            for (int i = 1; i <= 3; i++) camp.Items.Add(StockItem(m, 0x48, i));
            var ai = m.Create<Burntime.Remaster.AI.DosAiState>(new object[]
            {
                player, new Burntime.Remaster.AI.AiSettings { Difficulty = 1 }
            });
            ai.Turn(); Equal(2, camp.Items.Count, "one deletion in first turn");
            ai.Turn(); Equal(1, camp.Items.Count, "one deletion in second turn");
            camp.IsCity = false; camp.Source.Water = 0;
            ai.Turn(); Equal(1, camp.Items.Count, "dry neutral camp skips maintenance");
            return 0;
        });

        yield return Int("DOS refresh retains first food leftovers and uses type membership", 0, () =>
        {
            var m = new StateManager(null!); var stock = m.Create<ItemList>();
            var core = Enumerable.Range(0x3d, 4).Select(t => StockItem(m, t, 0).Type).ToArray();
            Item coreAtEnd = m.Create<Item>(core[0]);
            Item junk = StockItem(m, 0x48, 2);
            Item food1 = StockItem(m, 0x36, 3), food2 = StockItem(m, 0x36, 4);
            stock.Add(food2); stock.Add(junk); stock.Add(food1); stock.Add(coreAtEnd);
            OriginalTraderRefresh.Dos(stock, core, 3, StockTitle, _ => 1, _ => { }, food1.Type);
            Equal(true, stock.Contains(coreAtEnd), "core type survives independent of display position");
            Equal(false, stock.Contains(junk), "first non-core removed");
            Equal(true, stock.Contains(food1), "later food remains");
            Equal(false, stock.Contains(food2), "first local food removed");
            return 0;
        });
        yield return Int("DOS removes at most one non-core item", 0, () =>
        {
            var m = new StateManager(null!); var stock = m.Create<ItemList>();
            stock.Add(StockItem(m, 0x48, 2)); stock.Add(StockItem(m, 0x49, 1));
            OriginalTraderRefresh.Dos(stock, Array.Empty<ItemType>(), 1, StockTitle, _ => 1,
                _ => throw new Exception("unexpected restock"), StockItem(m, 0x36, 0).Type);
            Equal(1, stock.Count, "single preliminary removal");
            Equal(0x49, StockTitle(stock[0].Type), "first local non-core item removed");
            return 0;
        });
        yield return Int("DOS removal is not replenished in the same visit", 0, () =>
        {
            var m = new StateManager(null!); var stock = m.Create<ItemList>();
            var knife = StockItem(m, 0x3d, 1); stock.Add(knife);
            var additions = new List<ItemType>();
            OriginalTraderRefresh.Dos(stock, new[] { knife.Type }, 1, StockTitle, _ => 0,
                additions.Add, StockItem(m, 0x36, 0).Type);
            Equal(0, stock.Count, "existing item removed on modulo-matching day");
            Equal(0, additions.Count, "no immediate recreation");
            OriginalTraderRefresh.Dos(stock, new[] { knife.Type }, 5, StockTitle, _ => 0,
                additions.Add, StockItem(m, 0x36, 0).Type);
            Equal(1, additions.Count, "missing item replenished on next matching day");
            return 0;
        });
        yield return Int("DOS all six assortment entries and duplicate meat", 0, () =>
        {
            var m = new StateManager(null!); var stock = m.Create<ItemList>();
            var types = new[] { 0x3e, 0x3f, 0x40, 0x41, 0x42, 0x46 }
                .Select(t => StockItem(m, t, 0).Type).ToArray();
            // Three meat records leave one after the two preliminary removals.
            for (int i = 0; i < 3; i++) stock.Add(StockItem(m, 0x36, i + 1));
            var added = new List<int>();
            OriginalTraderRefresh.Dos(stock, types, 2, StockTitle, _ => 1,
                t => added.Add(StockTitle(t)), StockItem(m, 0x36, 0).Type);
            Equal("62,66,70,54", string.Join(",", added), "every matching specialty, then meat despite existing meat");
            Equal(1, stock.Count, "remaining original meat");
            return 0;
        });
        yield return Int("Amiga two removal passes retain distinct cadence", 0, () =>
        {
            var m = new StateManager(null!); var stock = m.Create<ItemList>();
            stock.Add(StockItem(m, 0x40, 1)); stock.Add(StockItem(m, 0x45, 2));
            stock.Add(StockItem(m, 0x48, 3)); stock.Add(StockItem(m, 0x36, 4));
            var order = new List<int>();
            OriginalTraderRefresh.Amiga(stock, Array.Empty<ItemType>(), 0,
                StockTitle, _ => { order.Add(stock.Count); return 1; }, _ => { }, StockItem(m, 0x36, 0).Type);
            Equal("4,2,1", string.Join(",", order), "offset-five pass precedes offset-zero pass; food cleared");
            Equal(0, stock.Count, "all matching records removed, not only one");
            return 0;
        });
        yield return Int("Amiga removal preserves global phases with local trader order", 0, () =>
        {
            var m = new StateManager(null!); var a = m.Create<ItemList>(); var b = m.Create<ItemList>();
            a.Add(StockItem(m, 0x45, 3)); b.Add(StockItem(m, 0x45, 1));
            a.Add(StockItem(m, 0x40, 2)); b.Add(StockItem(m, 0x36, 4));
            var visits = new List<string>();
            OriginalTraderRefresh.AmigaRemove(new[] { a, b }, 0, StockTitle,
                _ => { visits.Add($"{a.Count}/{b.Count}"); return 1; });
            Equal("2/2,1/2,1/0", string.Join(",", visits), "whole-world offset-five before offset-zero");
            Equal(0, a.Count + b.Count, "all deletion passes finish before additions");
            return 0;
        });
        yield return Int("Amiga cleanup cursor continues across party members", 0, () =>
        {
            var m = new StateManager(null!); var camp = m.Create<Location>(); camp.Rooms = m.CreateLinkList<Room>();
            var first = m.Create<HazardCharacter>(); first.Items = m.Create<ItemList>();
            var second = m.Create<HazardCharacter>(); second.Items = m.Create<ItemList>();
            for (int i = 0; i < 5; i++) first.Items.Add(StockItem(m, 0x48, i + 10));
            camp.Items.Add(StockItem(m, 0x48, 1)); // spends first character's last scan slot
            camp.Items.Add(StockItem(m, 0x49, 2)); // generic discard flag survives into next character
            camp.Items.Add(StockItem(m, 0x3e, 3));
            OriginalItemRecords.CleanupAmiga(camp, new Character[] { first, second }, i => StockTitle(i.Type), 0);
            Equal(5, first.Items.Count, "discard consumes scan budget, not actual inventory capacity");
            Equal(1, second.Items.Count, "next character picks up axe");
            Equal(0x49, StockTitle(camp.Items[0].Type), "second generic item preserved across characters");
            return 0;
        });
        yield return Int("Amiga restocks duplicates and all matching specialties", 0, () =>
        {
            var m = new StateManager(null!); var stock = m.Create<ItemList>();
            var types = new[] { 0x40, 0x48, 0x41 }.Select(t => StockItem(m, t, 0).Type).ToArray();
            stock.Add(m.Create<Item>(types[0]));
            var added = new List<int>();
            OriginalTraderRefresh.Amiga(stock, types, 0, StockTitle, _ => 0,
                t => added.Add(StockTitle(t)), StockItem(m, 0x36, 0).Type);
            Equal(1, stock.Count, "existing specialty survives zero random roll");
            Equal("64,72,54", string.Join(",", added), "duplicate specialty, second specialty, meat");
            return 0;
        });
        yield return Int("Amiga cleanup transfers weapons, preserves production, bounded discard", 0, () =>
        {
            var m = new StateManager(null!); var camp = m.Create<Location>();
            camp.Rooms = m.CreateLinkList<Room>();
            var boss = m.Create<HazardCharacter>(); boss.Items = m.Create<ItemList>();
            foreach (var (title, slot) in new[] { (0x33, 1), (0x43, 2), (0x3d, 3), (0x48, 4), (0x44, 5) })
                camp.Items.Add(StockItem(m, title, slot));
            OriginalItemRecords.CleanupAmiga(camp, new Character[] { boss }, i => StockTitle(i.Type), 0x43);
            Equal(1, boss.Items.Count, "knife picked up");
            Equal("67,72", string.Join(",", camp.Items.Select(i => StockTitle(i.Type))), "selected trap and later generic loot preserved");
            return 0;
        });
        yield return Int("Amiga full party leaves food untouched", 0, () =>
        {
            var m = new StateManager(null!); var camp = m.Create<Location>();
            camp.Rooms = m.CreateLinkList<Room>();
            var boss = m.Create<HazardCharacter>(); boss.Items = m.Create<ItemList>();
            for (int i = 0; i < 6; i++) boss.Items.Add(StockItem(m, 0x48, 20 + i));
            camp.Items.Add(StockItem(m, 0x33, 1));
            OriginalItemRecords.CleanupAmiga(camp, new Character[] { boss }, i => StockTitle(i.Type), 0);
            Equal(1, camp.Items.Count, "no scan without carrying space");
            return 0;
        });
        yield return Int("Amiga opponent room loot remains untouched", 0, () =>
        {
            var m = new StateManager(null!); var camp = m.Create<Location>();
            camp.Rooms = m.CreateLinkList<Room>(); var room = m.Create<Room>(); camp.Rooms.Add(room);
            var player = m.Create<HazardPlayer>(new object[] { 0 });
            var enemy = m.Create<HazardPlayer>(new object[] { 1 }); camp.Player = enemy;
            var boss = m.Create<HazardCharacter>(); boss.Player = player; boss.Items = m.Create<ItemList>();
            room.Items.Add(StockItem(m, 0x33, 1)); camp.Items.Add(StockItem(m, 0x33, 2));
            OriginalItemRecords.CleanupAmiga(camp, new Character[] { boss }, i => StockTitle(i.Type), 0);
            Equal(1, room.Items.Count, "opponent room skipped"); Equal(0, camp.Items.Count, "ground still scanned");
            return 0;
        });
        yield return Int("original trader allocation exceeds carrying caps", 0, () =>
        {
            var m = new StateManager(null!); var game = m.Create<ClassicGame>(); m.Root = game;
            game.World = m.Create(() =>
            {
                var world = (ClassicWorld)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ClassicWorld));
                foreach (string field in new[] { "ID", "localID" })
                    typeof(StateObject).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(world, -1);
                return world;
            });
            game.World.AllCharacters = m.CreateLinkList<Character>();
            game.World.Locations = m.CreateLinkList<Location>();
            var trader = m.Create<Trader>(); trader.Items = m.Create<ItemList>(); trader.Items.MaxCount = 6;
            game.World.AllCharacters.Add(trader);
            ItemType type = StockItem(m, 0x36, 0).Type;
            for (int i = 0; i < 6; i++) trader.Items.Add(m.Create<Item>(type));
            OriginalItemRecords.AddTraderStock(trader, type);
            Equal(7, trader.Items.Count, "direct allocation bypasses cap");
            Equal(6, trader.Items.MaxCount, "human capacity setting preserved");
            trader.Items.MaxCount = ItemList.Infinite;
            while (trader.Items.Count < 1199) trader.Items.Add(m.Create<Item>(type));
            OriginalItemRecords.AddTraderStock(trader, type);
            Equal(1200, trader.Items.Count, "former global pool cap does not prevent creation");
            return 0;
        });
    }
}
