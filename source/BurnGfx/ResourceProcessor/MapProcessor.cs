using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;

namespace Burntime.Data.BurnGfx.ResourceProcessor
{
    public class MapProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID ID, ResourceManager ResourceManager)
        {
            MapData data = new MapData();
            data.DataName = ID;
            Map map = new Map(ID.File);

            data.Width = map.width;
            data.Height = map.height;
            data.TileSize = new Vector2(32, 32);

            data.Tiles = new MapTile[data.Width * data.Height];
            for (int i = 0; i < map.data.Length; i++)
            {
                data.Tiles[i].Section = (short)(map.data[i] & 0xff);
                data.Tiles[i].Item = (short)(map.data[i] >> 8);
            }

            data.Entrances = new MapEntrance[map.Doors.Count];
            for (int i = 0; i < map.Doors.Count; i++)
            {
                data.Entrances[i].Area = map.Doors[i].Area;
                if (map.Doors[i].RoomID < 4)
                    data.Entrances[i].RoomType = RoomType.Normal;
                else if (map.Doors[i].RoomID < 9)
                    data.Entrances[i].RoomType = RoomType.WaterSource;
                else if (map.Doors[i].RoomID == 9)
                    data.Entrances[i].RoomType = RoomType.Rope;
                else if (map.Doors[i].RoomID == 16)
                    data.Entrances[i].RoomType = RoomType.Church;
                else if (map.Doors[i].RoomID == 0xd || map.Doors[i].RoomID == 0x11)
                    data.Entrances[i].RoomType = RoomType.Trader;
                else if (map.Doors[i].RoomID == 14 || map.Doors[i].RoomID == 23)
                    data.Entrances[i].RoomType = RoomType.Pub;
                else if (map.Doors[i].RoomID == 20)
                    data.Entrances[i].RoomType = RoomType.Doctor;
                else if (map.Doors[i].RoomID == 22 || map.Doors[i].RoomID == 24)
                    data.Entrances[i].RoomType = RoomType.Restaurant;
                else
                    data.Entrances[i].RoomType = RoomType.Scene;
                data.Entrances[i].Background = map.Doors[i].RoomID;
                data.Entrances[i].TitleId = "burn?" + (660 + map.Doors[i].RoomID);
            }

            data.Mask = new PathMask(data.Width * 4, data.Height * 4, 8);

            foreach (Vector2 pos in new Rect(0, 0, data.Width, data.Height))
            {
                String res = "burngfxtilemask@zei_" + data[pos].Section.ToString("D3") + ".raw?" + data[pos].Item;
                IDataProcessor processor = ResourceManager.GetDataProcessor("burngfxtilemask");
                TileMaskData tile = processor.Process(res, ResourceManager) as TileMaskData;

                foreach (Vector2 sub in new Rect(0, 0, 4, 4))
                {
                    //data.Mask[pos * 4 + sub] = !(pos.x == 0 && sub.x == 0 || pos.y == 0 && sub.y == 0 || (pos.x == data.Width - 1 && sub.x == 3) || (pos.y == data.Height - 1 && sub.y == 3));
                    //data.Mask[pos * 4 + sub] &= tile[sub];
                    data.Mask[pos * 4 + sub] = tile[sub];
                }
            }

            return data;
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "burngfxmap" }; }
        }
    }
}
