using System;
using System.Linq;
using System.Collections.Generic;
using Burntime.Data.BurnGfx;
using Burntime.Data.BurnGfx.Save;
using Burntime.Remaster.Logic.Interaction;

namespace Burntime.Remaster.Logic.Generation
{
    /// <summary>
    /// Creates locations in original order from gamdat/burnmap files.
    /// </summary>
    public class OriginalLocationCreator : IGameObjectCreator
    {
        public void Create(ClassicGame game)
        {
            var gamdat = LogicFactory.GetParameter<Burntime.Data.BurnGfx.Save.SaveGame>("gamdat");
            if (gamdat == null)
                return;

            var container = game.Container;

            for (int i = 1; i <= gamdat.Locations.Length; i++)
            {
                var city = gamdat.Locations[i - 1];

                Location loc = container.Create<Location>();
                loc.Id = game.World.Locations.Count;
                loc.Source.Water = city.WaterSource;
                loc.Source.Reserve = city.Water;
                loc.Source.Capacity = city.WaterCapacity;
                loc.Production = city.Producing == -1 ? null : game.Productions[city.Producing];
                loc.AvailableProducts = (int[])city.Production.Clone();
                if (city.Danger != 0)
                    loc.Danger = Danger.Instance((city.Danger == 3) ? "radiation" : "gas", city.DangerAmount);
                loc.IsCity = city.IsCity;
                loc.EntryPoint = city.EntryPoint;
                loc.Ways = (from x in city.Ways where x != -1 select x).ToArray();
                loc.WayLengths = (from x in city.WayLengths where x > 0 select x).ToArray();
                loc.NeighborIds = (from x in city.Neighbors where x != -1 select x).ToArray();

                loc.Map = container.Create<Map>(new object[] { "maps/mat_" + i.ToString("D3") + ".burnmap??" + i });

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
            }

            foreach (Location location in game.World.Locations)
            {
                foreach (int id in location.NeighborIds)
                    location.Neighbors.Add(game.World.Locations[id]);
            }
        }
    }
}
