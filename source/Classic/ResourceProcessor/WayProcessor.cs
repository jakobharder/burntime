using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Burntime.Platform.IO;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Data.BurnGfx;
using Burntime.Classic.Logic.Generation;

namespace Burntime.Classic.ResourceProcessor
{
    public class WayProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID id, ResourceManager resourceManager)
        {
            var burngfxProcessor = new Burntime.Data.BurnGfx.ResourceProcessor.WayProcessor();
            WayData burngfx = burngfxProcessor.Process("gam.dat", resourceManager) as WayData;

            WayData data = new WayData();
            data.DataName = id;

            ConfigFile config = new ConfigFile();
            config.Open(FileSystem.GetFile(id.File));

            Map map = LogicFactory.GetParameter<Map>("mainmap");
            int cityCount = map.Entrances.Length;

            //int cityCount = config[""].GetInt("cities");
            int wayCount = config[""].GetInt("ways");
            Vector2 offset = config[""].GetVector2("burngfx_offset");

            data.Cross = new CrossWay[System.Math.Max(cityCount, burngfx.Cross.Length)];

            // add cross ways defined in burngfx
            for (int i = 0; i < burngfx.Cross.Length; i++)
                data.Cross[i] = burngfx.Cross[i];

            // add cross ways defined in ways.txt
            for (int i = 0; i < cityCount; i++)
            {
                if (data.Cross[i].Ways == null)
                {
                    data.Cross[i].Ways = new int[0];
                }
            }

            List<Way> ways = new List<Way>();

            // add ways defined in burngfx
            foreach (Way way in burngfx.Ways)
            {
                Way add = way;
                add.Position += offset;
                ways.Add(add);
            }

            // add ways defined in ways.txt
            for (int i = 0; i < wayCount; i++)
            {
                Way way = new Way();
                way.Start = config["ways"].GetInts("way" + i)[0];
                way.End = config["ways"].GetInts("way" + i)[1];

                way.Position = new Vector2();
                way.Position.x = System.Math.Min(map.Entrances[way.Start].Area.Center.x, map.Entrances[way.End].Area.Center.x);
                way.Position.y = System.Math.Min(map.Entrances[way.Start].Area.Center.y, map.Entrances[way.End].Area.Center.y);

                way.Images = new Platform.Graphics.Sprite[1];
                way.Images[0] = resourceManager.GetImage(config["images"].GetString("way" + i), ResourceLoadType.Delayed);

                ways.Add(way);

#warning // TODO make more performant
                data.Cross[way.Start].Ways = data.Cross[way.Start].Ways.Concat(new int [] { ways.Count - 1}).ToArray();
                data.Cross[way.End].Ways = data.Cross[way.End].Ways.Concat(new int[] { ways.Count - 1 }).ToArray();
            }

            //// add ways defined in burnmap
            //for (int i = 0; i < map.MapData.Ways.Length; i++)
            //{
            //    Way way = map.MapData.Ways[i];

            //    ways.Add(way);

            //    data.Cross[way.Start].Ways = data.Cross[way.Start].Ways.Push(ways.Count - 1);
            //    data.Cross[way.End].Ways = data.Cross[way.End].Ways.Push(ways.Count - 1);
            //}

            data.Ways = ways.ToArray();

            return data;
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "ways" }; }
        }
    }

}
