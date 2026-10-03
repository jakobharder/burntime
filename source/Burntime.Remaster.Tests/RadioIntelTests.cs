using System.Collections.Generic;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Tests;

using static Program;

static class RadioIntelTests
{
    internal static IEnumerable<Case<int>> RadioCases()
    {
        yield return Int("no defenders", 0, () => RadioIntel.ThreatLevel(0));
        yield return Int("low defense", 1, () => RadioIntel.ThreatLevel(25));
        yield return Int("moderate defense", 2, () => RadioIntel.ThreatLevel(26));
        yield return Int("high defense", 3, () => RadioIntel.ThreatLevel(75));
        yield return Int("extreme defense", 4, () => RadioIntel.ThreatLevel(100));
        yield return Int("defense rating is capped", 4, () => RadioIntel.ThreatLevel(500));
        yield return Int("stored radios provide health and local scouting through the network", 0, () =>
        {
            var (game, player, enemy, manager) = EncounterPlayers(RuleSet.Extended);
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
            var current = manager.Create<Location>();
            var station = manager.Create<Location>();
            var target = manager.Create<Location>();
            var distant = manager.Create<Location>();
            player.Location = current;
            station.Player = player;
            game.World.Locations.Add(station);
            station.Neighbors.Add(target);
            target.Neighbors.Add(distant);
            var room = manager.Create<Room>();
            station.Rooms.Add(room);
            room.Items.Add(TestItem(manager, "item_two_way_radio",
                functions: ItemFunction.RemoteIntel));
            Equal(true, RadioIntel.CanSeeHealth(player, current), "local health needs no radio");
            Equal(false, RadioIntel.CanSeeHealth(player, station), "remote station needs receiver");
            Equal(false, RadioIntel.IsAvailable(player, target), "station needs receiver");
            var receiverRoom = manager.Create<Room>();
            current.Rooms.Add(receiverRoom);
            var receiver = TestItem(manager, "item_two_way_radio",
                functions: ItemFunction.RemoteIntel);
            receiverRoom.Items.Add(receiver);
            Equal(true, RadioIntel.CanSeeHealth(player, station), "stored receiver enables remote health");
            Equal(true, RadioIntel.IsAvailable(player, target), "remote station scouts its neighbor");
            Equal(false, RadioIntel.IsAvailable(player, distant), "reports do not relay beyond neighbors");
            receiverRoom.Items.Remove(receiver);
            player.Character.Items.Add(receiver);
            Equal(true, RadioIntel.CanSeeHealth(player, station), "carried receiver enables remote health");
            Equal(true, RadioIntel.IsAvailable(player, target), "carried receiver enables remote scouting");
            station.Player = enemy;
            Equal(false, RadioIntel.CanSeeHealth(player, station), "enemy storage does not reveal health");
            Equal(false, RadioIntel.IsAvailable(player, target), "enemy station is excluded");
            current.Neighbors.Add(target);
            Equal(true, RadioIntel.IsAvailable(player, target), "carried radio scouts local neighbor");
            player.Character.Items.Remove(receiver);
            receiverRoom.Items.Add(receiver);
            Equal(true, RadioIntel.IsAvailable(player, target), "current storage scouts local neighbor");
            receiverRoom.Items.Remove(receiver);
            receiverRoom.Items.Add(TestItem(manager, "item_defective_two_way_radio"));
            Equal(false, RadioIntel.IsAvailable(player, target), "defective radio cannot receive");
            station.Player = player;
            Equal(false, RadioIntel.CanSeeHealth(player, station), "defective receiver cannot reveal health");
            return 0;
        });
        yield return Int("radio availability follows carrier, target and rules", 0, () =>
        {
            var (_, player, _, manager) = EncounterPlayers(RuleSet.Extended);
            var target = manager.Create<Location>();
            player.Location = manager.Create<Location>();
            player.Location.Neighbors.Add(target);
            player.Character.Items.Add(TestItem(manager, "item_two_way_radio",
                functions: ItemFunction.RemoteIntel));
            Equal(true, RadioIntel.IsAvailable(player, target),
                "carried extended radio reports neighboring camp");
            target.Player = player;
            Equal(false, RadioIntel.IsAvailable(player, target),
                "owned camp keeps normal info");
            target.Player = null;
            target.IsCity = true;
            Equal(false, RadioIntel.IsAvailable(player, target),
                "cities do not receive camp reports");
            var (_, classicPlayer, _, classicManager) = EncounterPlayers(RuleSet.Classic);
            var classicTarget = classicManager.Create<Location>();
            classicPlayer.Character.Items.Add(TestItem(classicManager, "item_two_way_radio"));
            Equal(false, RadioIntel.IsAvailable(classicPlayer, classicTarget),
                "classic radio remains unchanged");
            return 0;
        });
    }
}
