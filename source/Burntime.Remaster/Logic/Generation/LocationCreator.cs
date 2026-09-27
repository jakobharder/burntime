using System;
using System.Collections.Generic;
using System.Linq;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Platform.IO;
using Burntime.Framework;
using Burntime.Data.BurnGfx;
using Burntime.Data.BurnGfx.Save;
using Burntime.Remaster.Logic.Interaction;

namespace Burntime.Remaster.Logic.Generation
{
    /// <summary>
    /// Creates locations in original order from gamdat/burnmap files.
    /// </summary>
    public class LocationCreator : IGameObjectCreator
    {
        private void updateBurngfxLocation(int id, int[] ways, int[] wayLengths, Framework.States.StateLinkList<Location> locations)
        {
            for (int i = 0; i < ways.Length; i++)
            {
                if (ways[i] < id)
                {
                    bool found = false;
                    foreach (int alreadyAdded in locations[ways[i]].NeighborIds)
                    {
                        if (alreadyAdded == id)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        locations[ways[i]].NeighborIds = locations[ways[i]].NeighborIds.Concat(new int[] { id }).ToArray();
                        locations[ways[i]].WayLengths = locations[ways[i]].WayLengths.Concat(new int[] { wayLengths[i] }).ToArray();
                    }
                }
            }
        }

        internal static void ApplyEnvironment(Location location, ConfigFile config, IResourceManager resources)
        {
            location.AvailableProducts = config[""].GetInts("available_products");
            string danger = config[""].GetString("danger");
            location.Danger = string.IsNullOrEmpty(danger) ? null : resources.GetData(danger) as Danger;
        }

        public void Create(ClassicGame game)
        {
            var resources = LogicFactory.GetParameter<IResourceManager>("resource");
            var container = game.Container;

            // for the time being only add to existing locations
            for (int i = game.World.Locations.Count + 1; i < game.World.Map.Entrances.Length + 1; i++)
            {
                ConfigFile cfg = new ConfigFile();
                cfg.Open("maps/mat_" + i.ToString("D3") + ".txt");

                int water = cfg[""].GetInt("water_refresh");
                int[] neighborIds = cfg[""].GetInts("ways");
                int[] wayLengths = cfg[""].GetInts("way_lengths");
                string dangerName = cfg[""].GetString("danger");
                Map map = container.Create<Map>(new object[] { "maps/mat_" + i.ToString("D3") + ".burnmap??4" });
                Location loc = Location.Create(container, new Location.Params(
                    Id: i - 1,
                    Map: map,
                    Water: water,
                    WaterReserve: water,
                    WaterCapacity: cfg[""].GetInt("water_capacity"),
                    Production: null,
                    AvailableProducts: cfg[""].GetInts("available_products"),
                    Danger: string.IsNullOrEmpty(dangerName) ? null : resources.GetData(dangerName) as Danger,
                    IsCity: cfg[""].GetBool("city"),
                    EntryPoint: cfg[""].GetVector2("entry_point"),
                    Ways: Array.Empty<int>(),
                    WayLengths: wayLengths,
                    NeighborIds: neighborIds));

                // in case of a burngfx location we need to add new ways
                updateBurngfxLocation(loc.Id, neighborIds, wayLengths, game.World.Locations);

                game.World.Locations += loc;
                i++;
            }

            // update neighbor links
            foreach (Location location in game.World.Locations)
            {
                for (int k = 0; k < location.NeighborIds.Length; k++)
                {
                    if (location.NeighborIds[k] != -1)
                    {
                        var neighbor = game.World.Locations[location.NeighborIds[k]];
                        // only add if not already in the list
                        if (!location.Neighbors.Contains(neighbor))
                            location.Neighbors.Add(neighbor);
                    }
                }
            }
        }
    }
}
