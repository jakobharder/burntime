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

                Map map = container.Create<Map>(new object[] { "maps/mat_" + i.ToString("D3") + ".burnmap??" + i });
                Location loc = Location.Create(container, new Location.Params(
                    Id: game.World.Locations.Count,
                    Map: map,
                    Water: city.WaterSource,
                    WaterReserve: city.Water,
                    WaterCapacity: city.WaterCapacity,
                    Production: city.Producing == -1 ? null : game.Productions[city.Producing],
                    AvailableProducts: (int[])city.Production.Clone(),
                    Danger: city.Danger == 0
                        ? null
                        : Danger.Instance(city.Danger == 3 ? "radiation" : "gas", city.DangerAmount),
                    IsCity: city.IsCity,
                    EntryPoint: city.EntryPoint,
                    Ways: city.Ways.Where(x => x != -1).ToArray(),
                    WayLengths: city.WayLengths.Where(x => x > 0).ToArray(),
                    NeighborIds: city.Neighbors.Where(x => x != -1).ToArray()));

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
