using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using Burntime.Platform.IO;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Data.BurnGfx;
using System.Linq;

namespace Burntime.Deluxe.ResourceProcessor
{
    class BurnmapProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID id, IResourceManager ResourceManager)
        {
            int map = 0;
            if (id.Custom != null)
                map = int.Parse(id.Custom);

            IDataProcessor processor = ResourceManager.GetDataProcessor("burngfxmap");
            MapData raw = null;
            if (FileSystem.ExistsFile(System.IO.Path.GetFileNameWithoutExtension(id.File) + ".raw"))
                raw = processor.Process(System.IO.Path.GetFileNameWithoutExtension(id.File) + ".raw??" + map, ResourceManager) as MapData;

            MapData data = new MapData();

            Burntime.Platform.IO.File file = Burntime.Platform.IO.FileSystem.GetFile(id.File);
            BinaryReader reader = new BinaryReader(file);
            if (reader.ReadString() != "Burntime Map")
                return null;
            String ver = reader.ReadString();
            if (ver != "0.2")
                return null;

            // header
            data.Width = reader.ReadInt32();
            data.Height = reader.ReadInt32();
            data.TileSize = Vector2.One * reader.ReadInt32();

            data.Tiles = new Burntime.Data.BurnGfx.MapTile[data.Width * data.Height];
            data.Mask = new Burntime.Data.BurnGfx.PathMask(data.Width * 4, data.Height * 4, 8);

            // set indices
            List<string> tileSetNames = new List<string>();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                tileSetNames.Add(reader.ReadString());

            ConfigFile settings = new ConfigFile();
            if (FileSystem.ExistsFile(id.File.Replace(".burnmap", ".txt")))
                settings.Open(FileSystem.GetFile(id.File.Replace(".burnmap", ".txt")));
            else
                settings = null;

            //Dictionary<String, int> indices2 = new Dictionary<string, int>();
            //for (int i = 0; i < TileSets.Count; i++)
            //    indices2.Add(TileSets[i].Name, i);

            // tiles
            for (int y = 0; y < data.Height; y++)
            {
                for (int x = 0; x < data.Width; x++)
                {
                    byte _id = reader.ReadByte();
                    byte subset = reader.ReadByte();
                    byte set = reader.ReadByte();

                    if (subset == 0)
                    {
                    }
                    else
                    {
                        Burntime.Data.BurnGfx.MapTile tile = new Burntime.Data.BurnGfx.MapTile();
                        tile.Item = _id;
                        tile.Section = subset;
                        tile.Set = set;

                        data.Tiles[x + y * data.Width] = tile;

                        String maskFile = "gfx/tiles/" + subset.ToString("D3") + "_" + _id.ToString("D2") + ".txt";
                        if (Burntime.Platform.IO.FileSystem.ExistsFile(maskFile))
                        {
                            Burntime.Platform.IO.File maskfile = Burntime.Platform.IO.FileSystem.GetFile(maskFile);
                            TextReader maskreader = new StreamReader(maskfile);

                            for (int k = 0; k < 4; k++)
                            {
                                String line = maskreader.ReadLine();
                                if (line.Length < 4)
                                    continue;

                                char[] chrs = line.ToCharArray();
                                data.Mask[4 * x + 0, 4 * y + k] = (chrs[0] != '1');
                                data.Mask[4 * x + 1, 4 * y + k] = (chrs[1] != '1');
                                data.Mask[4 * x + 2, 4 * y + k] = (chrs[2] != '1');
                                data.Mask[4 * x + 3, 4 * y + k] = (chrs[3] != '1');
                            }

                            maskreader.Close();
                            maskfile.Close();
                        }
                        else
                        {
                            for (int k = 0; k < 4; k++)
                            {
                                data.Mask[4 * x + 0, 4 * y + k] = true;
                                data.Mask[4 * x + 1, 4 * y + k] = true;
                                data.Mask[4 * x + 2, 4 * y + k] = true;
                                data.Mask[4 * x + 3, 4 * y + k] = true;
                            }
                        }
                    }
                }
            }

            foreach (Vector2 pos in new Rect(0, 0, data.Width, data.Height))
            {
                if (data[pos].Section >= 90)
                {
                    foreach (Vector2 sub in new Rect(0, 0, 4, 4))
                    {
                        data.Mask[pos * 4 + sub] &= !(pos.x == 0 && sub.x == 0 || pos.y == 0 && sub.y == 0 ||
                            (pos.x == data.Width - 1 && sub.x == 3) || (pos.y == data.Height - 1 && sub.y == 3));
                    }
                }
                else
                {
                    String res = "burngfxtilemask@zei_" + data[pos].Section.ToString("D3") + ".raw?" + data[pos].Item;
                    IDataProcessor p = ResourceManager.GetDataProcessor("burngfxtilemask");
                    TileMaskData tile = p.Process(res, ResourceManager) as TileMaskData;

                    foreach (Vector2 sub in new Rect(0, 0, 4, 4))
                    {
                        data.Mask[pos * 4 + sub] = !(pos.x == 0 && sub.x == 0 || pos.y == 0 && sub.y == 0 ||
                            (pos.x == data.Width - 1 && sub.x == 3) || (pos.y == data.Height - 1 && sub.y == 3));
                        data.Mask[pos * 4 + sub] &= tile[sub];
                    }
                }
            }

            // fixed items
            //string[] items = settings[""].GetStrings("fixed_item");
            //bool[] items_room = settings[""].GetBools("fixed_item_room");
            //Vector2[] items_pos = settings[""].GetVector2s("fixed_item_pos");

            //data.FixedItems = new FixedItem[items.Length];
            //for (int i = 0; i < items.Length; i++)
            //{
            //    data.FixedItems[i].Item = items[i];
            //    data.FixedItems[i].Room = items_room[i] ? items_pos[i].x : -1;
            //    data.FixedItems[i].Position = items_room[i] ? Vector2.Zero : items_pos[i];
            //}

            // entrances
            count = reader.ReadByte();
            data.Entrances = new MapEntrance[count];

            for (int i = 0; i < count; i++)
            {
                MapEntrance e = new MapEntrance();

                // take data from original Burntime as default
#warning        // TODO this should be only temporary
                if (raw != null && raw.Entrances.Length > i)
                    e = raw.Entrances[i];

//                e.RoomType = Burntime.Data.BurnGfx.RoomType.Normal;

                e.Area.Left = reader.ReadInt32();
                e.Area.Top = reader.ReadInt32();
                e.Area.Width = reader.ReadInt32();
                e.Area.Height = reader.ReadInt32();

//#warning TODO insert proper images and titles
//                e.Background = 0;// settings["room" + i].GetString("image");
                if (settings != null)
                    e.TitleId = settings["room" + i].GetString("title");
//                //e.Key = settings["room" + i].GetString("key");

                data.Entrances[i] = e;
            }

            //if (ver == "0.2")
            //{
            //    count = reader.ReadInt32();
            //    for (int i = 0; i < count; i++)
            //    {
            //        Way way = new Way();
            //        way.Days = reader.ReadByte();
            //        way.Entrance[0] = reader.ReadByte();
            //        way.Entrance[1] = reader.ReadByte();

            //        int wpc = reader.ReadByte();
            //        for (int j = 0; j < wpc; j++)
            //        {
            //            Point pt = new Point();
            //            pt.X = reader.ReadInt32();
            //            pt.Y = reader.ReadInt32();
            //            way.Points.Add(pt);
            //        }
            //    }
            //}

            file.Close();

            int customTileSetIndex = tileSetNames.IndexOf("_");

            for (int i = 0; i < data.Tiles.Length; i++)
            {
                if (data.Tiles[i].Set > 0)
                {
                    if (customTileSetIndex == data.Tiles[i].Set)
                    {
                        data.Tiles[i].Image = ResourceManager.GetImage($"pngsheet@maps/{Path.GetFileNameWithoutExtension(id.File)}_tiles.png?{data.Tiles[i].Item}?32x32", ResourceLoadType.Delayed);
                    }
                    if (data.Tiles[i].Image is not null)
                        continue;
                }
                
                if (data.Tiles[i].Section >= 90)
                    data.Tiles[i].Image = ResourceManager.GetImage("gfx/tiles/" + data.Tiles[i].Section.ToString("D3") + "_" + data.Tiles[i].Item.ToString("D2") + ".png", ResourceLoadType.Delayed);
                else
                    data.Tiles[i].Image = ResourceManager.GetImage("burngfxtile@zei_" + data.Tiles[i].Section.ToString("D3") + ".raw?" + data.Tiles[i].Item.ToString() + "?" + map, ResourceLoadType.Delayed);
            }

            return data;
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "burnmap" }; }
        }
    }
}
