using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace MapEditor
{
    public class TileManager
    {
        static List<Tile> changed = new List<Tile>();

        public static bool Changed
        {
            get { return changed.Count != 0; }
        }

        public static void Save()
        {
            String path = "tiles";

            foreach (Tile tile in changed)
            {
                String maskFile = tile.SubSet.ToString("D3") + "_" + tile.ID.ToString("D2") + ".txt";
                TextWriter writer = new StreamWriter(path + "\\" + tile.Set + "\\" + maskFile);

                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        writer.Write(tile.Mask[y * 4 + x] ? "1" : "0");
                    }
                    writer.WriteLine();
                }

                writer.Close();
            }

            changed.Clear();
        }


        public static void Change(Tile tile)
        {
            changed.Add(tile);
        }

        public static void LoadTiles(List<TileSet> TileSets)
        {
            TileSet classicSet = new TileSet();
            classicSet.Name = "classic";

            TileSets.Add(classicSet);

            String path = "tiles";
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            String[] sets = Directory.GetDirectories(path);

            foreach (String set in sets)
            {
                if (Path.GetFileName(set) == "classic")
                    continue;

                TileSet tiles = new TileSet();
                tiles.Name =  Path.GetFileName(set);

                bool stop = false;
                for (int j = 1; !stop && j < 200; j++)
                {
                    for (int i = 0; i < 64; i++)
                    {
                        String file = j.ToString("D3") + "_" + i.ToString("D2") + ".png";
                        if (!File.Exists(set + "\\" + file))
                        {
                            //stop = true;
                            //break;
                            continue;
                        }

                        Tile tile = new Tile();
                        tile.Image = new Bitmap(set + "\\" + file);
                        tile.Size = tile.Image.Size;
                        tile.Set = tiles.Name;
                        tile.ID = (byte)i;
                        tile.SubSet = (byte)j;

                        String maskFile = j.ToString("D3") + "_" + i.ToString("D2") + ".txt";
                        if (File.Exists(set + "\\" + maskFile))
                        {
                            TextReader reader = new StreamReader(set + "\\" + maskFile);

                            for (int k = 0; k < 4; k++)
                            {
                                String line = reader.ReadLine();
                                if (line.Length < 4)
                                    continue;

                                char[] chrs = line.ToCharArray();
                                tile.Mask[k * 4 + 0] = (chrs[0] == '1');
                                tile.Mask[k * 4 + 1] = (chrs[1] == '1');
                                tile.Mask[k * 4 + 2] = (chrs[2] == '1');
                                tile.Mask[k * 4 + 3] = (chrs[3] == '1');
                            }

                            reader.Close();
                        }

                        tiles.Tiles.Add(tile);
                    }
                }

                TileSets.Add(tiles);
            }
        }

        public static void LoadClassicTiles(TileSet ClassicSet, String Path)
        {
            String path = "tiles";

            Burntime.Platform.IO.FileSystem.AddPackage("burntime", Path + "\\burn_gfx");

            Dictionary<int, int> dicColorTables = new Dictionary<int, int>();
            dicColorTables.Add(6, 23);
            dicColorTables.Add(7, 23);
            dicColorTables.Add(16, 23);
            dicColorTables.Add(10, 14);
            dicColorTables.Add(11, 14);
            dicColorTables.Add(15, 14);
            dicColorTables.Add(12, 13);
            dicColorTables.Add(13, 13);
            dicColorTables.Add(18, 2);
            dicColorTables.Add(25, 26);
            dicColorTables.Add(26, 26);
            dicColorTables.Add(32, 89);
            dicColorTables.Add(44, 84);
            dicColorTables.Add(46, 83);
            dicColorTables.Add(47, 83);
            dicColorTables.Add(29, 35);
            dicColorTables.Add(45, 82);
            dicColorTables.Add(48, 81);
            dicColorTables.Add(49, 80);
            dicColorTables.Add(50, 80);
            dicColorTables.Add(17, 33);

            for (int j = 1; j < 52; j++)
            {
                if (null == Burntime.Platform.IO.FileSystem.GetFile("zei_" + j.ToString("D3") + ".raw"))
                    continue;

                int mapId = 0;
                if (dicColorTables.ContainsKey(j))
                    mapId = dicColorTables[j];

                Burntime.Data.BurnGfx.TileSet tileset = new Burntime.Data.BurnGfx.TileSet("zei_" + j.ToString("D3") + ".raw", mapId);
                tileset.Load();

                for (int i = 0; i < 63; i++)
                {
                    Burntime.Data.BurnGfx.Tile tile = tileset.Tiles[i];
                    if (tile != null)
                    {
                        Bitmap bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                        BitmapData data = bmp.LockBits(new Rectangle(0, 0, 32, 32), ImageLockMode.ReadWrite, PixelFormat.Format32bppRgb);
                        tile.Render(data.Scan0);
                        bmp.UnlockBits(data);

                        Tile t = new Tile();
                        t.Set = ClassicSet.Name;
                        t.Image = bmp;

                        t.SubSet = (byte)j;
                        t.ID = (byte)i;
                        t.Size = new Size(32, 32);

                        int mask = tile.Mask;
                        for (int k = 15; k >= 0; k--, mask >>= 1)
                            t.Mask[k] = (mask & 1) == 1;

                        String maskFile = j.ToString("D3") + "_" + i.ToString("D2") + ".txt";
                        if (File.Exists(path + "\\classic\\" + maskFile))
                        {
                            TextReader reader = new StreamReader(path + "\\classic\\" + maskFile);

                            for (int k = 0; k < 4; k++)
                            {
                                String line = reader.ReadLine();
                                if (line.Length < 4)
                                    continue;

                                char[] chrs = line.ToCharArray();
                                t.Mask[k * 4 + 0] = (chrs[0] == '1');
                                t.Mask[k * 4 + 1] = (chrs[1] == '1');
                                t.Mask[k * 4 + 2] = (chrs[2] == '1');
                                t.Mask[k * 4 + 3] = (chrs[3] == '1');
                            }

                            reader.Close();
                        }

                        //if (t.GetHashCode() != 0)
                        {
                            //bmp.Save("output/" + j.ToString("D3") + "_" + i.ToString("D2") + ".png");
                            ClassicSet.Tiles.Add(t);
                        }
                    }
                }
            }
        }
    }
}
