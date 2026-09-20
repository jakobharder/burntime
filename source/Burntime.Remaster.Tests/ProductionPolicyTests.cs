using System;
using System.Collections.Generic;
using System.Linq;
using Burntime.Framework;
using Burntime.Framework.States;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;

namespace Burntime.Remaster.Tests;

using static Program;

static class ProductionPolicyTests
{
    internal static IEnumerable<Case<int>> ProductionPolicyCases()
    {
        yield return Int("keyboard movement latches automatic camera follow", 0, () =>
        {
            InputAction[] movementActions =
            {
                InputAction.MoveUp,
                InputAction.MoveDown,
                InputAction.MoveLeft,
                InputAction.MoveRight
            };
            foreach (InputAction action in movementActions)
                Equal(true, LocationScene.StartsAutomaticCameraFollow(action),
                    $"{action} starts camera follow");

            Equal(false, LocationScene.StartsAutomaticCameraFollow(InputAction.PanCameraUp),
                "manual camera pan does not start character follow");
            return 0;
        });

        yield return Int("unwalkable positions recover only on scene entry", 0, () =>
        {
            var m = new StateManager(null!);
            var mask = new Burntime.Data.BurnGfx.PathMask(5, 5, 8);
            mask[1, 2] = true;
            mask[3, 2] = true;
            var blockedPosition = new Burntime.Platform.Vector2(20, 20);
            var recoveredPosition = new Burntime.Platform.Vector2(12, 20);

            Equal(recoveredPosition,
                Burntime.Remaster.PathFinding.PathState.GetNearestWalkablePosition(
                    mask, blockedPosition),
                "nearest walkable cell is selected deterministically");
            Equal(recoveredPosition,
                Burntime.Remaster.PathFinding.PathState.GetNearestWalkablePosition(
                    mask, recoveredPosition),
                "walkable positions remain unchanged");

            var simple = m.Create<Burntime.Remaster.PathFinding.SimplePath>();
            simple.MoveTo = blockedPosition;
            Equal(blockedPosition, simple.Process(mask, blockedPosition, 0.016f),
                "simple path does not recover during movement");
            Equal(recoveredPosition, simple.GetSceneEntryPosition(mask, blockedPosition),
                "simple path recovers on scene entry");

            var manual = m.Create<Burntime.Remaster.PathFinding.ManualPath>();
            manual.MoveTo = blockedPosition;
            Equal(blockedPosition, manual.Process(mask, blockedPosition, 0.016f),
                "manual path does not recover during movement");
            Equal(recoveredPosition, manual.GetSceneEntryPosition(mask, blockedPosition),
                "manual path recovers on scene entry");

            var edgeMask = new Burntime.Data.BurnGfx.PathMask(3, 3, 8);
            edgeMask[1, 1] = true;
            var validSlidePosition = new Burntime.Platform.Vector2(7, 8);
            Equal(validSlidePosition,
                manual.GetSceneEntryPosition(edgeMask, validSlidePosition),
                "scene entry preserves centered-sampled edge slide positions");
            return 0;
        });

        foreach (bool clearByItem in new[] { false, true })
            yield return Int($"clearing populated item window, instance setter {clearByItem}", 0, () =>
            {
                // Reproduce a previously displayed slot without loading graphics.
                var window = (Burntime.Remaster.GUI.ItemWindow)System.Runtime.CompilerServices.RuntimeHelpers
                    .GetUninitializedObject(typeof(Burntime.Remaster.GUI.ItemWindow));
                typeof(Burntime.Remaster.GUI.ItemWindow).GetField("displayedSprite",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .SetValue(window, "gfx/pistol_loaded_2.png");
                window.Background = (Burntime.Framework.GUI.GuiImage)System.Runtime.CompilerServices.RuntimeHelpers
                    .GetUninitializedObject(typeof(Burntime.Framework.GUI.GuiImage));
                if (clearByItem) window.Item = null;
                else window.ItemID = "";
                Equal(true, window.Background == null, "clear removes image without resolving a null resource");
                Equal(true, window.TooltipText == null, "clear removes tooltip");
                Equal("", window.ItemID, "empty slot has no item");
                window.ItemID = "";
                Equal(true, window.Background == null, "repeated clear remains safe");
                return 0;
            });
        yield return Int("New Village environment refresh removes saved hazard and enables rats", 0, () =>
        {
            var m = new StateManager(null!); var camp = m.Create<Location>();
            camp.Danger = new Burntime.Remaster.Logic.Interaction.Danger("radiation", 10, "10", null!) { DataName = "old-radiation" };
            camp.AvailableProducts = Array.Empty<int>();
            var config = new Burntime.Platform.IO.ConfigFile();
            config.Open(System.IO.File.OpenRead(ResourceFile("maps/MAT_038.txt")));
            LocationCreator.ApplyEnvironment(camp, config, null!);
            Equal(true, camp.Danger == null, "old saved radiation cleared");
            Equal(true, camp.AvailableProducts.SequenceEqual(new[] { 1 }), "rat production potential");
            return 0;
        });
        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
            foreach (string tool in new[] { "item_knife", "item_rat_trap", "item_snake_trap", "item_trap" })
                yield return Int($"{rule} / {tool} inventory and room production", 0, () =>
                {
                    var m = new StateManager(null!);
                    var config = new Burntime.Platform.IO.ConfigFile();
                    config.Open(System.IO.File.OpenRead(ResourceFile(GameDefinitions.Get(rule).ProductionPath)));
                    var section = config[tool];
                    var food = TestItem(m, section.GetString("produce"), food: 3).Type;
                    var production = m.Create(() => new Production(section.GetInt("maxcombination"),
                        section.GetInts("amount"), section.GetInts("amount2"), food, 0,
                        section.GetBool("allow_inventory")));
                    bool allowed = false;
                    Equal(allowed, production.AllowInventory, "all built-in rules require installed tools");
                    var camp = m.Create<Location>(); camp.Rooms = m.CreateLinkList<Room>();
                    var room = m.Create<Room>(); camp.Rooms.Add(room);
                    var player = m.Create<HazardPlayer>(new object[] { 0 }); camp.Player = player;
                    var guard = m.Create<HazardCharacter>(); guard.Player = player;
                    guard.Items = m.Create<ItemList>(); camp.Characters.Add(guard);
                    var item = TestItem(m, tool); item.Type.Production = production; guard.Items.Add(item);
                    camp.Production = production;
                    Equal(allowed ? 1 : 0, camp.GetProductionToolCount(production), "inventory count");
                    Equal(section.GetInts("amount")[allowed ? 1 : 0], camp.GetFoodProductionRate().FoodPerDay, "actual camp output");
                    Equal(allowed ? 1 : 0, Burntime.Remaster.AI.CampEconomy.ProductionToolCount(camp, production), "AI agrees");
                    guard.Items.Remove(item); room.Items.Add(item);
                    Equal(1, camp.GetProductionToolCount(production), "installed tool works in every rule");
                    Equal(section.GetInts("amount")[1], camp.GetFoodProductionRate().FoodPerDay, "room output");
                    // Settings refresh must replace the saved flag, not retain an obsolete value.
                    production.ApplySettings(section.GetInt("maxcombination"), section.GetInts("amount"),
                        section.GetInts("amount2"), !allowed);
                    Equal(!allowed, production.AllowInventory, "save refresh replaces flag");
                    return 0;
                });
        yield return Int("custom maggot alternatives can opt into inventory tools", 0, () =>
        {
            var m = new StateManager(null!); var camp = m.Create<Location>(); camp.Rooms = m.CreateLinkList<Room>();
            var guard = m.Create<HazardCharacter>(); guard.Player = m.Create<HazardPlayer>(new object[] { 0 });
            guard.Items = m.Create<ItemList>(); camp.Characters.Add(guard);
            var p = m.Create(() => new Production(1, new[] { 1, 2 }, Array.Empty<int>(),
                TestItem(m, "item_maggots", food: 3).Type, 0, allowInventory: true));
            foreach (string name in new[] { "item_knife", "item_axe", "item_pitchfork" })
            {
                guard.Items.Clear(); var item = TestItem(m, name); item.Type.Production = p; guard.Items.Add(item);
                Equal(1, camp.GetProductionToolCount(p), name);
            }
            return 0;
        });
        foreach (RuleSet rule in Enum.GetValues<RuleSet>())
            yield return Int($"{rule} technician maintenance bonus", 0, () =>
            {
                var m = new StateManager(null!);
                var game = m.Create<ClassicGame>(); m.Root = game; game.SetRules(rule);
                var camp = m.Create<Location>(); camp.Rooms = m.CreateLinkList<Room>();
                var room = m.Create<Room>(); camp.Rooms.Add(room);
                var player = m.Create<HazardPlayer>(new object[] { 0 }); camp.Player = player;
                var food = TestItem(m, "food", food: 3).Type;
                var production = m.Create(() => new Production(1, new[] { 0, 3 },
                    new[] { 0, 3 }, food, 0));
                var tool = TestItem(m, "tool"); tool.Type.Production = production;
                room.Items.Add(tool); camp.Production = production;

                var technician = m.Create<HazardCharacter>();
                technician.Player = player; technician.Class = CharClass.Technician;
                camp.Characters.Add(technician);

                int expectedBonus = game.RuleBook.Settings.TechnicianFoodBonus;
                Production.Rate rate = camp.GetFoodProductionRate();
                Equal(3 + expectedBonus, rate.FoodPerDay, "configured total output");
                Equal(expectedBonus, rate.MaintenanceBonus, "reported maintenance contribution");

                var second = m.Create<HazardCharacter>();
                second.Player = player; second.Class = CharClass.Technician;
                camp.Characters.Add(second);
                Equal(3 + expectedBonus, camp.GetFoodProductionRate().FoodPerDay,
                    "multiple technicians do not stack");

                camp.IsCity = true;
                Equal(3, camp.GetFoodProductionRate().FoodPerDay, "cities receive no bonus");
                camp.IsCity = false; room.Items.Remove(tool);
                rate = camp.GetFoodProductionRate();
                Equal(0, rate.FoodPerDay, "maintenance cannot create production");
                Equal(0, rate.MaintenanceBonus, "inactive production reports no bonus");
                return 0;
            });
        yield return Int("automatic production upgrades while manual production stays pinned", 0, () =>
        {
            var m = new StateManager(null!);
            var game = m.Create<ClassicGame>(); m.Root = game;
            var camp = m.Create<Location>(); camp.Rooms = m.CreateLinkList<Room>();
            var room = m.Create<Room>(); camp.Rooms.Add(room);
            camp.Player = m.Create<HazardPlayer>(new object[] { 0 });

            var rats = m.Create(() => new Production(2, new[] { 0, 2, 5 },
                Array.Empty<int>(), TestItem(m, "item_rats", food: 5).Type, 0));
            var snakes = m.Create(() => new Production(1, new[] { 0, 3 },
                Array.Empty<int>(), TestItem(m, "item_snake", food: 7).Type, 1));
            game.Productions.Add(rats); game.Productions.Add(snakes);
            camp.AvailableProducts = new[] { 0, 1 };

            var ratTrap = TestItem(m, "item_rat_trap"); ratTrap.Type.Production = rats;
            var snakeTrap = TestItem(m, "item_snake_trap"); snakeTrap.Type.Production = snakes;
            room.Items.Add(ratTrap); room.Items.Add(snakeTrap);

            Equal(true, camp.IsProductionAutomatic, "automatic is the default");
            camp.RefreshFoodProductionSelection();
            Equal(snakes, camp.Production, "automatic selection chooses the higher yield");

            camp.SelectProduction(snakes);
            room.Items.Add(m.Create<Item>(ratTrap.Type));
            camp.RefreshFoodProductionSelection();
            Equal(snakes, camp.Production, "manual selection remains pinned while productive");

            room.Items.Remove(snakeTrap);
            camp.RefreshFoodProductionSelection();
            Equal(true, camp.IsProductionAutomatic, "unproductive manual selection returns to automatic");
            Equal(rats, camp.Production, "automatic fallback selects the productive traps");
            return 0;
        });
        yield return Int("stationed NPC prefers its lowest-value inventory food before storage", 0, () =>
        {
            var m = new StateManager(null!);
            var camp = m.Create<Location>();
            camp.Rooms = m.CreateLinkList<Room>();
            var room = m.Create<Room>(); camp.Rooms.Add(room);
            var player = m.Create<HazardPlayer>(new object[] { 0 }); camp.Player = player;
            var guard = m.Create<HazardCharacter>(); guard.Player = player; guard.Location = camp;
            camp.Characters.Add(guard);
            Item inventoryHigh = TestItem(m, "inventory_high", food: 9);
            Item inventoryLow = TestItem(m, "inventory_low", food: 5);
            Item storedLowest = TestItem(m, "stored_lowest", food: 3);
            guard.Items.Add(inventoryHigh); guard.Items.Add(inventoryLow); room.Items.Add(storedLowest);

            Item? selected = guard.FindAccessibleFood(out IItemCollection? owner);
            Equal(inventoryLow, selected, "lowest inventory food selected");
            Equal(true, ReferenceEquals(guard.Items, owner), "own inventory checked first");

            guard.Items.Clear();
            selected = guard.FindAccessibleFood(out owner);
            Equal(storedLowest, selected, "storage used when inventory has no food");
            Equal(true, ReferenceEquals(room.Items, owner), "storage owns fallback item");
            return 0;
        });
        yield return Int("travelling NPC uses lowest food across group without storage", 0, () =>
        {
            var m = new StateManager(null!);
            var camp = m.Create<Location>();
            camp.Rooms = m.CreateLinkList<Room>();
            var room = m.Create<Room>(); camp.Rooms.Add(room);
            Item storedLowest = TestItem(m, "stored_lowest", food: 3); room.Items.Add(storedLowest);
            var player = m.Create<HazardPlayer>(new object[] { 0 }); player.Location = camp;
            var first = m.Create<HazardCharacter>(); first.Player = player; first.Location = camp;
            var second = m.Create<HazardCharacter>(); second.Player = player; second.Location = camp;
            player.Party.Add(first); player.Party.Add(second);
            Item groupHigh = TestItem(m, "group_high", food: 9);
            Item groupLow = TestItem(m, "group_low", food: 5);
            first.Items.Add(groupHigh); second.Items.Add(groupLow);

            Item? selected = first.FindAccessibleFood(out IItemCollection? owner);
            Equal(groupLow, selected, "lowest group food selected");
            Equal(true, ReferenceEquals(second.Items, owner), "group inventory owns selected item");

            first.Items.Clear(); second.Items.Clear();
            selected = first.FindAccessibleFood(out owner);
            Equal<Item?>(null, selected, "camp storage is unavailable to travelling group");
            Equal<IItemCollection?>(null, owner, "no storage owner returned");
            return 0;
        });
        yield return Int("guard equipment preserves installed knife trap but can take a spare", 0, () =>
        {
            var m = new StateManager(null!);
            var camp = m.Create<Location>(); camp.Rooms = m.CreateLinkList<Room>();
            var room = m.Create<Room>(); camp.Rooms.Add(room);
            var guard = m.Create<HazardCharacter>(); guard.Location = camp; guard.Items = m.Create<ItemList>(); guard.Items.MaxCount = 6;
            var production = m.Create(() => new Production(1, new[] { 1, 2 }, Array.Empty<int>(),
                TestItem(m, "item_maggots", food: 3).Type, 0));
            camp.Production = production;
            var knife = TestItem(m, "item_knife", damage: 25); knife.Type.Production = production;
            room.Items.Add(knife);
            Equal<Item?>(null, Burntime.Remaster.AI.WeaponLoadout.EquipStoredCampWeapon(guard, _ => true), "installed knife stays in room");
            Equal(1, camp.GetProductionToolCount(production), "production remains equipped");
            var spare = m.Create<Item>(knife.Type); room.Items.Add(spare);
            Equal(2, camp.GetProductionToolCount(production), "two installed tools before equipment");
            Equal(25, knife.Type.WeaponPriority, "knife test priority");
            Equal(true, Burntime.Remaster.AI.WeaponLoadout.EquipStoredCampWeapon(guard, _ => true) != null, "spare can arm guard");
            Equal(1, camp.GetProductionToolCount(production), "inventory weapon is not a second trap");
            return 0;
        });
    }

    internal static IEnumerable<Case<int>> AmmunitionLifecycleCases()
    {
        yield return Int("Extended traders replace one rifle slot with a pistol", 0, () =>
        {
            var config = new Burntime.Platform.IO.ConfigFile();
            config.Open(System.IO.File.OpenRead(ResourceFile("rules/extended/trader.txt")));
            int rifles = 0, pistols = 0;
            for (int id = 179; id <= 200; id++) {
                var stock = config["trader"].GetStrings(id.ToString());
                int r = stock.Count(item => item == "item_loaded_rifle");
                int p = stock.Count(item => item == "item_loaded_pistol");
                Equal(true, r + p <= 1, "at most one gun slot per seller");
                rifles += r; pistols += p;
            }
            Equal(1, rifles, "one rifle seller remains");
            Equal(1, pistols, "one pistol seller replaces second rifle seller");
            Equal(true, config["trader"].GetStrings("179").Contains("item_loaded_rifle"), "Marty keeps rifle");
            Equal(true, config["trader"].GetStrings("199").Contains("item_loaded_pistol"), "Ivan stocks pistol");
            Equal(7, config["trader"].GetStrings("199").Length, "Ivan assortment size unchanged");
            return 0;
        });
        yield return Int("pistol trades damage for consistency at equal priority", 0, () =>
        {
            var config = new Burntime.Platform.IO.ConfigFile();
            config.Open(System.IO.File.OpenRead(ResourceFile("rules/extended/items.txt")));
            var rifle = config["item_loaded_rifle"]; var pistol = config["item_loaded_pistol"];
            Equal(rifle.GetInt("weapon_priority"), pistol.GetInt("weapon_priority"), "equal equipment priority");
            for (int tier = 0; tier < 4; tier++) {
                var r = rifle.GetInts("damage").Skip(tier * 4).Take(4).ToArray();
                var p = pistol.GetInts("damage").Skip(tier * 4).Take(4).ToArray();
                double ratio = p.Average() / r.Average();
                Equal(true, ratio >= 0.85 && ratio <= 0.90, "pistol average 10–15% lower");
                Equal(true, p.Min() > r.Min() && p.Max() < r.Max(), "tighter spread at each tier");
            }
            return 0;
        });
        foreach (string owned in new[] { "item_loaded_rifle", "item_loaded_pistol", "item_unloaded_pistol" })
            foreach (string purchased in new[] { "item_loaded_rifle", "item_loaded_pistol" })
                yield return Int($"shared firearm limit: {owned} then {purchased}", 0, () =>
                {
                    var m = new StateManager(null!);
                    var owner = m.Create<HazardCharacter>(); owner.Items = m.Create<ItemList>();
                    var recipient = m.Create<HazardCharacter>(); recipient.Items = m.Create<ItemList>();
                    owner.Items.Add(TestItem(m, owned));
                    var gun = TestItem(m, purchased, damage: 15, ammo: 6);
                    var party = new Character[] { owner, recipient };
                    Equal(true, Burntime.Remaster.AI.AiItemPool.IsFirearm(gun.Type), "purchasing firearm classification");
                    for (int difficulty = 0; difficulty < 3; difficulty++)
                        Equal(difficulty == 2, Burntime.Remaster.AI.EquipmentPlanning.CanAssignFirearm(
                            recipient, party, gun.Type, Burntime.Remaster.AI.AiPolicy.ForDifficulty(difficulty)),
                            "rifles and pistols share Easy/Normal cap; Hard unrestricted");
                    return 0;
                });
        yield return Int("ammo icons, counts and reload", 0, () =>
        {
            var m = new StateManager(null!);
            ItemType Make(string id, int capacity, string sprite, string? last = null)
                => m.Create<ItemType>(new Burntime.Remaster.Logic.Data.ItemTypeData {
                    DataName = id, Sprite = sprite, LastRoundSprite = last,
                    AmmoValue = capacity, Class = Array.Empty<string>(), Protection = Array.Empty<string>() });
            var empty = Make("item_unloaded_rifle", 0, "empty");
            var loaded = Make("item_loaded_rifle", 6, "two", "one");
            loaded.Empty = empty;
            var gun = m.Create<Item>(loaded);
            for (int rounds = 6; rounds > 0; rounds--) {
                Equal(rounds == 1 ? "one" : "two", gun.Sprite, "ammo display threshold");
                Equal($"item_loaded_rifle ({rounds}/6)", gun.TooltipText, "exact ammo tooltip");
                gun.Use();
            }
            Equal("empty", gun.Sprite, "empty sprite");
            Equal(0, gun.AmmoValue, "empty counter");
            gun.Reload(loaded);
            Equal(6, gun.AmmoValue, "reload fills rifle");
            return 0;
        });
        foreach (var (rule, id, initial, perAmmo) in new[]
        {
            (RuleSet.Dos, "item_loaded_rifle", 6, 6),
            (RuleSet.Amiga, "item_loaded_rifle", 6, 6),
            (RuleSet.Extended, "item_loaded_rifle", 6, 6),
            (RuleSet.Extended, "item_loaded_pistol", 6, 6)
        })
            foreach (int spare in new[] { 0, 2 })
                yield return Int($"{rule} {id}, {spare} spare ammunition", initial + spare * perAmmo, () =>
                {
                    var m = new StateManager(null!); var fighter = m.Create<HazardCharacter>(); fighter.Items = m.Create<ItemList>();
                    var config = new Burntime.Platform.IO.ConfigFile();
                    config.Open(System.IO.File.OpenRead(ResourceFile(GameDefinitions.Get(rule).ItemsPath.Split('@')[1])));
                    ItemType MakeType(string name)
                    {
                        var section = config[name];
                        int ammo = section.GetInt("ammo");
                        var type = TestItem(m, name, damage: ammo > 0 ? 1 : 0, ammo: ammo).Type;
                        string next = section.GetString("empty");
                        if (!string.IsNullOrEmpty(next))
                        {
                            StateLink<ItemType> link = MakeType(next);
                            typeof(ItemType).GetField("empty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(type, link);
                        }
                        return type;
                    }
                    Item gun = m.Create<Item>(MakeType(id)); fighter.Items.Add(gun);
                    for (int i = 0; i < spare; i++) fighter.Items.Add(TestItem(m, "item_ammunition"));
                    int shots = 0;
                    while (gun.DamageValue > 0 && shots < 30) { fighter.UseOriginalWeapon(gun); shots++; }
                    Equal(false, fighter.Items.Any(i => i.ID == "item_ammunition"), "spares consumed");
                    Equal(true, gun.ID.StartsWith("item_unloaded_"), "weapon ends unloaded");
                    return shots;
                });
    }
}
