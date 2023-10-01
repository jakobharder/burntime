using System;
using System.Collections.Generic;
using System.Linq;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Platform.IO;
using Burntime.Framework;
using Burntime.Data.BurnGfx;
using Burntime.Data.BurnGfx.Save;
using Burntime.Classic.Logic.Interaction;

namespace Burntime.Classic.Logic.Generation
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

        public void Create(ClassicGame game)
        {
            var resources = LogicFactory.GetParameter<IResourceManager>("resource");
            var container = game.Container;

            // for the time being only add to existing locations
            for (int i = game.World.Locations.Count + 1; i < game.World.Map.Entrances.Length + 1; i++)
            {
                ConfigFile cfg = new ConfigFile();
                cfg.Open("maps/mat_" + i.ToString("D3") + ".txt");

                Location loc = container.Create<Location>();
                loc.Id = i - 1;
                loc.Source.Water = cfg[""].GetInt("water_refresh");
                loc.Source.Reserve = loc.Source.Water;
                loc.Source.Capacity = cfg[""].GetInt("water_capacity");
                loc.Production = null;// city.Producing == -1 ? null : game.Productions[city.Producing];
                loc.AvailableProducts = new int[] { };// (int[])city.Production.Clone();
                loc.Danger = resources.GetData(cfg[""].GetString("danger")) as Danger;
                loc.IsCity = cfg[""].GetBool("city");
                loc.EntryPoint = cfg[""].GetVector2("entry_point");

                loc.Ways = new int[] { };
                loc.NeighborIds = cfg[""].GetInts("ways");
                loc.WayLengths = cfg[""].GetInts("way_lengths");

                // in case of a burngfx location we need to add new ways
                updateBurngfxLocation(loc.Id, loc.NeighborIds, loc.WayLengths, game.World.Locations);

                loc.Map = container.Create<Map>(new object[] { "maps/mat_" + i.ToString("D3") + ".burnmap??4" });

                loc.Rooms = container.CreateLinkList<Room>();

                for (int j = 0; j < loc.Map.Entrances.Length; j++)
                {
                    RoomType type = loc.Map.Entrances[j].RoomType;

                    Room room = container.Create<Room>();
                    room.IsWaterSource = type == RoomType.WaterSource;
                    if (type != RoomType.Normal && type != RoomType.Rope && type != RoomType.WaterSource)
                        room.Items.MaxCount = 0;
                    else
                        room.Items.MaxCount = room.IsWaterSource ? 8 : 32;
                    room.EntryCondition.MaxDistanceOnMap = 15;
                    if (loc.Map.Entrances[j].RoomType == RoomType.Rope)
                    {
                        room.EntryCondition.MaxDistanceOnMap = 75;
                        room.EntryCondition.RequiredItem = game.ItemTypes["item_rope"];
                    }
                    room.EntryCondition.RegionOnMap = loc.Map.Entrances[j].Area;
                    room.EntryCondition.HasRegionOnMap = true;
                    room.TitleId = loc.Map.Entrances[j].TitleId;
                    loc.Rooms += room;
                }

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
