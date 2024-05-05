using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Remaster;
using Burntime.Remaster.Logic;
using Burntime.Remaster.Logic.Generation;
using System.Linq;

namespace Burntime.Classic.Logic.Generation;

internal class ItemSpawner
{
    readonly ClassicGame _game;
    readonly Data.BurnGfx.Save.SaveGame _gamdat;
    readonly GameSettings _settings;

    public ItemSpawner(ClassicGame game, Data.BurnGfx.Save.SaveGame gamdat, GameSettings settings)
    {
        _game = game;
        _gamdat = gamdat;
        _settings = settings;
    }

    public void SpawnAtPlayerLocation()
    {
        GameSettings.ItemGeneration start = _settings.GetItemGeneration("random_items_start");

        foreach (Player player in _game.Player.Cast<Player>())
        {
            Location location = player.Location;
            location.StoreItemsRandom(start.GenerateAll(_game.ItemTypes));
        }
    }

    public void SpawnInAllLocations()
    {
        GameSettings.ItemGeneration ground = _settings.GetItemGeneration("random_items_ground");
        GameSettings.ItemGeneration room = _settings.GetItemGeneration("random_items_room");
        GameSettings.ItemGeneration danger = _settings.GetItemGeneration("random_items_danger_location");
        GameSettings.ItemGeneration closed = _settings.GetItemGeneration("random_items_closed_room");

        foreach (Location location in _game.World.Locations)
        {
            // add ground items
            var groundItems = ground.GenerateAll(_game.ItemTypes);
            foreach (var item in groundItems)
                location.DropItemRandom(item);

            if (location.IsCity)
                continue;

            // add room items
            location.StoreItemsRandom(room.GenerateAll(_game.ItemTypes));

            // add special danger location items
            if (location.Danger != null)
            {
                var dangerItems = danger.GenerateAll(_game.ItemTypes);
                foreach (var item in dangerItems)
                {
                    // insert items in room or drop it outside
                    if (Burntime.Platform.Math.Random.Next() % 2 == 0)
                        location.StoreItemRandom(item);
                    else
                        location.DropItemRandom(item);
                }
            }

            // add items to closed rooms (e.g. rooms only accessible with rope)
            foreach (Room r in location.Rooms)
            {
                if (r.EntryCondition != null && r.EntryCondition.RequiredItem != null)
                {
                    var closedItems = closed.GenerateAll(_game.ItemTypes);
                    foreach (var item in closedItems)
                        r.Items.Add(item);
                }
            }
        }
    }

    public void SpawnRegionItems()
    {
        var entry = _settings.GetRegionItem(0);
        for (int entryIndex = 0; entry is not null && entry != ConfigSection.NullSection; entry = _settings.GetRegionItem(++entryIndex))
        {
            var generator = GameSettings.ItemGeneration.FromString(entry.GetString("items"), entry.GetString("rate"));
            var locations = entry.GetInts("locations");
            if (locations.Length <= 0)
                continue;

            var items = generator.GenerateAll(_game.ItemTypes);
            foreach (var item in items)
            {
                var locationIndex = locations[Platform.Math.Random.Next(0, locations.Length)] - 1;
                if (locationIndex < 0 || locationIndex >= _game.World.Locations.Count)
                    continue;

                var location = _game.World.Locations[locationIndex];
                location.StoreItemRandom(item);
            }
        }
    }
}
