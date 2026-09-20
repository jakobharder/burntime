using System.Collections.Generic;
using System.Linq;
using Burntime.Platform;
using Burntime.Remaster.AI;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster.Tests;

using static Program;

static class ItemFunctionTests
{
    internal static IEnumerable<Case<int>> FunctionCases()
    {
        yield return Int("item type exposes configured functions", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            Item item = TestItem(manager, "utility", functions:
                ItemFunction.RemoteIntel | ItemFunction.RestingSustenance |
                ItemFunction.CreatureDeterrent | ItemFunction.MetalDetection);
            Equal(true, item.Type.HasFunction(ItemFunction.RemoteIntel), "remote intel");
            Equal(true, item.Type.HasFunction(ItemFunction.RestingSustenance), "sustenance");
            Equal(true, item.Type.HasFunction(ItemFunction.CreatureDeterrent), "deterrent");
            Equal(true, item.Type.HasFunction(ItemFunction.MetalDetection), "metal detection");
            return 0;
        });

        yield return Int("metal detector chance accumulates per travel day", 0, () =>
        {
            double oneDay = MetalDetectorScavenging.DiscoveryChance(1);
            double fourDays = MetalDetectorScavenging.DiscoveryChance(4);
            Equal(true, System.Math.Abs(oneDay - 0.01) < 0.000001, "one day");
            Equal(true, System.Math.Abs(fourDays - 0.03940399) < 0.000001, "four days");
            Equal(true, MetalDetectorScavenging.IsDiscovery(4, 0.039), "roll succeeds");
            Equal(false, MetalDetectorScavenging.IsDiscovery(4, 0.040), "roll fails");
            Equal(false, MetalDetectorScavenging.IsDiscovery(0, 0), "no travel");
            return 0;
        });

        yield return Int("creatures choose an unprotected party member", 0, () =>
        {
            var manager = new Burntime.Framework.States.StateManager(null!);
            var player = manager.Create<HazardPlayer>(new object[] { 0 });
            var protectedLeader = CreatePartyMember(manager, player, new Vector2(20, 0));
            protectedLeader.Items.Add(TestItem(manager, "item_skull",
                functions: ItemFunction.CreatureDeterrent));
            player.Character = protectedLeader;
            var follower = CreatePartyMember(manager, player, new Vector2(10, 0));
            player.Party.Add(follower);

            Equal(follower, CreatureMind.SelectAttackTarget(player, Vector2.Zero),
                "unprotected follower remains a target");
            follower.Items.Add(TestItem(manager, "item_bones",
                functions: ItemFunction.CreatureDeterrent));
            Equal<Character?>(null, CreatureMind.SelectAttackTarget(player, Vector2.Zero),
                "fully protected party is ignored");
            return 0;
        });

        yield return Int("Bible preserves supplies while resting", 0, () =>
        {
            var (rules, player, character, manager) = CreateCharacter();
            character.Items.Add(TestItem(manager, "item_bible",
                functions: ItemFunction.RestingSustenance));
            rules.TurnEmployedCharacter(character);
            Equal(3, character.Food, "resting food");
            Equal(2, character.Water, "resting water");
            return 0;
        });

        yield return Int("Bible does not prevent travel consumption", 0, () =>
        {
            var (rules, player, character, manager) = CreateCharacter();
            player.Traveling = true;
            character.Items.Add(TestItem(manager, "item_bible",
                functions: ItemFunction.RestingSustenance));
            rules.TurnEmployedCharacter(character);
            Equal(2, character.Food, "travel food");
            Equal(1, character.Water, "travel water");
            return 0;
        });

        yield return Int("resting Bible prevents starvation damage", 0, () =>
        {
            var (rules, _, character, manager) = CreateCharacter();
            character.Food = 0;
            character.Water = 0;
            character.Items.Add(TestItem(manager, "item_bible",
                functions: ItemFunction.RestingSustenance));
            rules.TurnEmployedCharacter(character);
            Equal(50, character.Health, "resting health");
            return 0;
        });

        yield return Int("Bible makes only Reststop a viable barren camp", 0, () =>
        {
            var (game, player, ai, manager) = CreateAi();
            var reststop = manager.Create<Location>();
            reststop.Id = CampEconomy.ReststopLocationId;
            reststop.AvailableProducts = System.Array.Empty<int>();
            reststop.Source.Water = 5;
            var other = manager.Create<Location>();
            other.Id = CampEconomy.ReststopLocationId - 1;
            other.AvailableProducts = System.Array.Empty<int>();
            other.Source.Water = 5;

            Equal(false, ai.CanClaim(reststop), "barren Reststop needs its Bible");
            Item bible = TestItem(manager, "item_bible",
                functions: ItemFunction.RestingSustenance);
            Equal(false, ai.Reserve.Insert(bible), "Bible stays out of abstract reserve");
            player.Character.Items.MaxCount = 6;
            player.Character.Items.Add(bible);
            Equal(true, ai.CanClaim(reststop), "Bible unlocks Reststop");
            Equal(false, ai.CanClaim(other), "Bible does not unlock other barren sites");
            return 0;
        });

        yield return Int("stored Bible is allocated only for a Reststop plan", 0, () =>
        {
            var (game, player, ai, manager) = CreateAi();
            player.Character.Items.MaxCount = 6;
            var camp = manager.Create<Location>();
            camp.Player = player;
            game.World.Locations.Add(camp);
            var room = manager.Create<Room>();
            room.Items.MaxCount = 8;
            camp.Rooms.Add(room);
            Item bible = TestItem(manager, "item_bible",
                functions: ItemFunction.RestingSustenance);
            room.Items.Add(bible);

            Equal(false, CampEconomy.HasPortableRestingSustenance(ai),
                "camp storage remains physical");
            Equal(true, CampManagement.PrepareReststopSettlement(ai),
                "Reststop plan allocates stored Bible");
            Equal(true, CampEconomy.HasPortableRestingSustenance(ai),
                "Bible becomes expedition cargo");
            Equal(false, room.Items.Contains(bible), "Bible leaves storage");
            return 0;
        });

        yield return Int("Reststop caretaker keeps the Bible", 0, () =>
        {
            var (game, player, ai, manager) = CreateAi();
            var reststop = manager.Create<Location>();
            reststop.Id = CampEconomy.ReststopLocationId;
            reststop.AvailableProducts = System.Array.Empty<int>();
            reststop.Source.Water = 5;
            reststop.Player = player;
            var room = manager.Create<Room>();
            room.Items.MaxCount = 8;
            reststop.Rooms.Add(room);
            var guard = CreatePartyMember(manager, player, Vector2.Zero);
            guard.Location = reststop;
            guard.NameId = "char?0";
            guard.Items.MaxCount = 6;
            Item bible = TestItem(manager, "item_bible",
                functions: ItemFunction.RestingSustenance);
            player.Character.Items.MaxCount = 6;
            player.Character.Items.Add(bible);
            Equal(true, ReferenceEquals(reststop.Player, ai.Player), "camp ownership");
            Equal(1, reststop.CampNPC.Count(), "living caretaker is present");
            Equal(true, CampEconomy.HasPortableRestingSustenance(ai),
                "Bible remains physically available");
            Equal(false, guard.Items.IsFull, "caretaker has room");

            Equal(true, CampManagement.EquipReststopCaretaker(ai, reststop, guard),
                "caretaker equipped");
            Equal(true, guard.HasItemFunction(ItemFunction.RestingSustenance),
                "Bible remains in caretaker inventory");
            Equal(0, CampEconomy.FoodSurplusPerDay(reststop),
                "Bible-supported caretaker does not create a food deficit");
            Equal(5, CampEconomy.WaterSurplusPerDay(reststop),
                "Bible-supported caretaker does not consume the well output");
            CampManagement.UnloadGarrisonBelongings(ai, reststop, guard);
            Equal(true, guard.HasItemFunction(ItemFunction.RestingSustenance),
                "later camp maintenance preserves Bible");
            return 0;
        });
    }

    static (GameRules Rules, HazardPlayer Player, HazardCharacter Character,
        Burntime.Framework.States.StateManager Manager) CreateCharacter()
    {
        var manager = new Burntime.Framework.States.StateManager(null!);
        var game = manager.Create(() => new ClassicGame());
        manager.Root = game;
        game.SetRules(RuleSet.Extended);
        var player = manager.Create<HazardPlayer>(new object[] { 0 });
        var character = manager.Create(() => new HazardCharacter());
        character.Player = player;
        character.Items = manager.Create<ItemList>();
        character.Class = CharClass.Boss;
        character.Health = 50;
        character.Food = 3;
        character.Water = 2;
        player.Character = character;
        return (game.RuleBook, player, character, manager);
    }

    static HazardCharacter CreatePartyMember(
        Burntime.Framework.States.StateManager manager, HazardPlayer player, Vector2 position)
    {
        var character = manager.Create(() => new HazardCharacter());
        character.Player = player;
        character.Items = manager.Create<ItemList>();
        character.Health = 100;
        character.Position = position;
        return character;
    }

    static (ClassicGame Game, HazardPlayer Player, ClassicAiState Ai,
        Burntime.Framework.States.StateManager Manager) CreateAi()
    {
        var manager = new Burntime.Framework.States.StateManager(null!);
        var game = manager.Create(() => new ClassicGame());
        manager.Root = game;
        game.SetRules(RuleSet.Extended);
        game.World = manager.Create(() =>
        {
            var world = (ClassicWorld)System.Runtime.CompilerServices.RuntimeHelpers
                .GetUninitializedObject(typeof(ClassicWorld));
            foreach (string field in new[] { "ID", "localID" })
                typeof(Burntime.Framework.States.StateObject).GetField(field,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!.SetValue(world, -1);
            return world;
        });
        game.World.Locations = manager.CreateLinkList<Location>();
        game.World.Players = manager.CreateLinkList<Player>();
        game.World.AllCharacters = manager.CreateLinkList<Character>();
        game.World.Traders = manager.CreateLinkList<Trader>();
        var player = manager.Create<HazardPlayer>(new object[] { 0 });
        var leader = CreatePartyMember(manager, player, Vector2.Zero);
        leader.Class = CharClass.Boss;
        player.Character = leader;
        player.Party.Add(leader);
        game.World.Players.Add(player);
        game.World.AllCharacters.Add(leader);
        var ai = manager.Create<ClassicAiState>(new object[]
            { player, new AiSettings { Difficulty = 1 } });
        return (game, player, ai, manager);
    }
}
