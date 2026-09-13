using System.Collections.Generic;
using Burntime.Data.BurnGfx;
using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Remaster.Logic;

namespace Burntime.Remaster.Tests;

using static Program;

static class LocationTests
{
    sealed class LocationTestMap : Map
    {
        protected override void InitInstance(object[] parameter)
            => mapData = (MapData)parameter[0];
    }

    internal static IEnumerable<Case<int>> LocationCases()
    {
        yield return Int("party arrival formation", 0, () =>
        {
            var manager = new StateManager(null!);
            var entry = new Vector2(100, 80);
            var location = manager.Create<Location>();
            location.EntryPoint = entry;
            Vector2[] offsets =
            {
                new(0, 0),
                new(0, -8),
                new(8, 0),
                new(-8, 0),
                new(0, 8)
            };

            for (int i = 0; i < Group.MAX_PEOPLE; i++)
                Equal(entry + offsets[i], location.GetEntryPosition(i),
                    $"member {i} arrival position");
            return 0;
        });

        foreach (bool hasNpcPosition in new[] { false, true })
            yield return Int($"entry placement, existing NPC position {hasNpcPosition}", 0, () =>
            {
                var manager = new StateManager(null!);
                var mask = new PathMask(100, 100, 8);
                mask[2, 1] = true;
                mask[3, 2] = true;
                mask[1, 2] = true;
                mask[2, 3] = true;
                mask[50, 50] = true;
                var camp = CreateLocation(manager, mask, new Vector2(16, 16));
                var npc = manager.Create<HazardCharacter>();
                npc.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
                npc.Location = camp;
                npc.Position = camp.EntryPoint;
                npc.Health = 43;
                var dog = manager.Create<HazardCharacter>();
                dog.Path = manager.Create<Burntime.Remaster.PathFinding.SimplePath>();
                dog.Class = CharClass.Dog;
                dog.Location = camp;
                dog.Position = camp.EntryPoint;
                var boss = manager.Create<HazardPlayer>(new object[] { 0 });
                var follower = manager.Create<HazardCharacter>();
                follower.Player = boss;
                boss.Group.Add(follower);
                follower.Location = camp;
                follower.Position = camp.EntryPoint;
                if (hasNpcPosition)
                {
                    var anchor = manager.Create<HazardCharacter>();
                    anchor.Location = camp;
                    anchor.Position = new Vector2(400, 400);
                }

                camp.PlaceUnpositionedResidents();
                var expectedNpc = hasNpcPosition ? new Vector2(400, 400) : camp.GetEntryPosition(1);
                var expectedDog = hasNpcPosition ? new Vector2(400, 400) : camp.GetEntryPosition(2);
                Equal(expectedNpc, npc.Position, "NPC position");
                Equal(expectedDog, dog.Position, "additional NPC position");
                Equal(43f, npc.ExactHealth, "health remains unchanged");
                Equal(camp.EntryPoint, follower.Position, "travelling group remains at entry");
                return 0;
            });

        yield return Int("preferred and available room selection", 0, () =>
        {
            var manager = new StateManager(null!);
            var camp = manager.Create<Location>();
            var first = manager.Create<Room>();
            var preferred = manager.Create<Room>();
            first.Items.MaxCount = 32;
            preferred.Items.MaxCount = 32;
            camp.Rooms.Add(first);
            camp.Rooms.Add(preferred);

            Item preferredItem = TestItem(manager, "preferred");
            camp.StoreItem(preferredItem, preferredRoom: preferred);
            Equal(preferredItem, preferred.Items[0], "preferred room receives item");

            preferred.Items.MaxCount = preferred.Items.Count;
            Item fallbackItem = TestItem(manager, "fallback");
            camp.StoreItem(fallbackItem, preferredRoom: preferred);
            Equal(fallbackItem, first.Items[0], "full preferred room falls back");
            return 0;
        });

        yield return Int("random storage can use every room", 0, () =>
        {
            Burntime.Platform.Math.SetRandomSeed(1);
            var manager = new StateManager(null!);
            var camp = manager.Create<Location>();
            var first = manager.Create<Room>();
            var last = manager.Create<Room>();
            first.Items.MaxCount = 32;
            last.Items.MaxCount = 32;
            camp.Rooms.Add(first);
            camp.Rooms.Add(last);

            for (int i = 0; i < 20; i++)
                camp.StoreItem(TestItem(manager, $"random-{i}"), randomRoom: true);

            Equal(true, first.Items.Count > 0, "first room selected");
            Equal(true, last.Items.Count > 0, "last room selected");
            return 0;
        });

        yield return Int("full storage drops at entry when map has no walkable cell", 0, () =>
        {
            var manager = new StateManager(null!);
            var entry = new Vector2(24, 32);
            var camp = CreateLocation(manager, new PathMask(2, 2, 8), entry);
            var fullRoom = manager.Create<Room>();
            fullRoom.Items.MaxCount = 0;
            camp.Rooms.Add(fullRoom);
            Item item = TestItem(manager, "ground-item");

            camp.StoreItem(item);

            Equal(1, camp.Items.Count, "item remains accessible");
            Equal(item, camp.Items[0], "dropped item");
            Equal(entry, camp.Items.MapObjects[0].Position, "entry fallback position");
            return 0;
        });
    }

    static Location CreateLocation(StateManager manager, PathMask mask, Vector2 entry)
    {
        var location = manager.Create<Location>();
        location.EntryPoint = entry;
        location.Map = manager.Create<LocationTestMap>(new MapData
        {
            DataName = "location-test",
            Mask = mask
        });
        return location;
    }
}
