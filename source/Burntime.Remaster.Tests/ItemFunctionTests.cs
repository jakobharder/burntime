using System.Collections.Generic;
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
}
