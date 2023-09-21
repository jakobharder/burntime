using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MapEditor
{
    public class MapDocument
    {
        public String FilePath;
        public String Title;
        public Size Size;
        public int TileSize;

        Tile[,] tiles;
        List<Entrance> entrances = new List<Entrance>();
        List<Way> ways = new List<Way>();

        public void SetTile(int x, int y, Tile tile)
        {
            tiles[x, y] = tile;
            Saved = false;
            AttachedView.UpdateMap();
            AttachedView.UpdateTitle();
        }

        public Tile GetTile(int x, int y)
        {
            return tiles[x, y];
        }

        public void AddEntrance(Rectangle Rect)
        {
            Entrance e = new Entrance();
            e.Rect = Rect;
            entrances.Add(e);

            Saved = false;
            if (AttachedView != null)
            {
                AttachedView.UpdateObjects();
                AttachedView.UpdateTitle();
            }
        }

        public void RemoveEntrance(int Index)
        {
            entrances.RemoveAt(Index);

            Saved = false;
            if (AttachedView != null)
            {
                AttachedView.UpdateObjects();
                AttachedView.UpdateTitle();
            }
        }

        public Entrance GetEntrance(int Index)
        {
            return entrances[Index];
        }

        public void SetEntrance(int Index, Entrance Entrance)
        {
            entrances[Index] = Entrance;

            Saved = false;
            if (AttachedView != null)
            {
                AttachedView.UpdateTitle();
                AttachedView.UpdateObjects();
            }
        }

        public int EntranceCount
        {
            get { return entrances.Count; }
        }

        public void AddWay(Way way)
        {
            ways.Add(way);

            Saved = false;
            if (AttachedView != null)
            {
                AttachedView.UpdateObjects();
                AttachedView.UpdateTitle();
            }
        }

        public void RemoveWay(int Index)
        {
            ways.RemoveAt(Index);

            Saved = false;
            if (AttachedView != null)
            {
                AttachedView.UpdateObjects();
                AttachedView.UpdateTitle();
            }
        }

        public Way GetWay(int index)
        {
            return ways[index];
        }

        public List<Way> Ways
        {
            get { return ways; }
            set
            {
                ways = value;

                Saved = false;
                if (AttachedView != null)
                {
                    AttachedView.UpdateObjects();
                    AttachedView.UpdateTitle();
                }
            }
        }

        public int WayCount
        {
            get { return ways.Count; }
        }

        IMapView AttachedView;

        bool saved;
        public bool Saved
        {
            get { return saved; }
            set { saved = value; if (AttachedView != null) AttachedView.UpdateTitle(); }
        }

        public MapDocument(IMapView view)
        {
            AttachedView = view;
        }

        public void New(Size Size, int TileSize)
        {
            this.Size = Size;
            this.TileSize = TileSize;
            Title = "unnamed";
            Saved = true;

            tiles = new Tile[Size.Width, Size.Height];
            AttachedView.UpdateMap();
            AttachedView.UpdateTitle();
        }

        public void Resize(Size Size, Point Offset, int TileSize)
        {
            Tile[,] newTiles = new Tile[Size.Width, Size.Height];

            for (int y = 0; y < Size.Height; y++)
            {
                for (int x = 0; x < Size.Width; x++)
                {
                    Point pt = new Point(x, y);
                    pt.X -= Offset.X;
                    pt.Y -= Offset.Y;

                    if (pt.X < 0 || pt.Y < 0 || pt.X >= this.Size.Width || pt.Y >= this.Size.Height)
                        continue;

                    newTiles[x, y] = tiles[pt.X, pt.Y];
                }
            }

            foreach (Entrance e in entrances)
            {
                e.Rect.X = e.Rect.X * TileSize / this.TileSize + Offset.X * TileSize;
                e.Rect.Y = e.Rect.Y * TileSize / this.TileSize + Offset.Y * TileSize;
                e.Rect.Width = e.Rect.Width * TileSize / this.TileSize;
                e.Rect.Height = e.Rect.Height * TileSize / this.TileSize;
            }

            tiles = newTiles;
            this.Size = Size;
            this.TileSize = TileSize;
            Saved = false;

            AttachedView.UpdateMap();
            AttachedView.UpdateTitle();
        }

        public void Import(Stream MatFile, int TileSize, TileSet TileSet)
        {
            Burntime.Platform.IO.File file = new Burntime.Platform.IO.File(MatFile);
            Burntime.Data.BurnGfx.Map map = new Burntime.Data.BurnGfx.Map(file);

            Size = new Size(map.width, map.height);
            this.TileSize = TileSize;
            Title = "unnamed";
            Saved = false;
            tiles = new Tile[Size.Width, Size.Height];

            for (int y = 0; y < Size.Height; y++)
            {
                for (int x = 0; x < Size.Width; x++)
                {
                    Tile tile = TileSet.Find((byte)(map.data[x + y * Size.Width] & 0xff), (byte)(map.data[x + y * Size.Width] >> 8));
                    tiles[x, y] = tile;
                }
            }

            foreach (Burntime.Data.BurnGfx.Door d in map.Doors)
            {
                Entrance e = new Entrance();
                e.Rect = new Rectangle(d.Area.Left, d.Area.Top, d.Area.Width, d.Area.Height);
                entrances.Add(e);
            }

            file.Close();

            AttachedView.UpdateMap();
            AttachedView.UpdateTitle();
        }

        public bool Export(String File)
        {
            Burntime.Platform.IO.File file = new Burntime.Platform.IO.File(new FileStream(File, FileMode.Open, FileAccess.ReadWrite));
            Burntime.Data.BurnGfx.Map map = new Burntime.Data.BurnGfx.Map(file);

            map.width = Size.Width;
            map.height = Size.Height;
            // check size
            map.data = new ushort[Size.Width * Size.Height];

            for (int y = 0; y < Size.Height; y++)
            {
                for (int x = 0; x < Size.Width; x++)
                {
                    map.data[x + y * Size.Width] = (ushort)(tiles[x, y].SubSet + (((ushort)tiles[x, y].ID) << 8));
                }
            }

            // check door count
            if (map.Doors.Count != entrances.Count)
            {
                MessageBox.Show("Entrance count must be the same as the original.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            for (int i = 0; i < map.Doors.Count; i++)
            {
                map.Doors[i].Area = new Burntime.Platform.Rect(entrances[i].Rect.Left, entrances[i].Rect.Top, entrances[i].Rect.Width, entrances[i].Rect.Height);
            }

            file.Seek(0, Burntime.Platform.IO.SeekPosition.Begin);
            map.SaveToRaw(file);

            file.Flush();
            file.Close();

            return true;
        }

        public void Import(String File, int Size, List<TileSet> TileSets, TileSet AddSet)
        {
            Bitmap bmp = new Bitmap(File);
            int tw = bmp.Width / Size;
            int th = bmp.Height / Size;

            Dictionary<int, List<Tile>> dic = new Dictionary<int, List<Tile>>();

            foreach (TileSet set in TileSets)
            {
                foreach (Tile tile in set.Tiles)
                {
                    int hash = tile.GetHashCode();
                    if (dic.ContainsKey(hash))
                    {
                        dic[hash].Add(tile);
                    }
                    else
                    {
                        List<Tile> list = new List<Tile>();
                        list.Add(tile);
                        dic.Add(tile.GetHashCode(), list);
                    }
                }
            }

            this.Size = new Size(tw, th);
            this.TileSize = Size;
            tiles = new Tile[tw, th];

            for (int y = 0; y < th; y++)
            {
                for (int x = 0; x < tw; x++)
                {
                    Bitmap tileImage = new Bitmap(Size, Size, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    Graphics g = Graphics.FromImage(tileImage);
                    g.DrawImage(bmp, new Rectangle(0, 0, Size, Size),
                        new Rectangle(x * Size, y * Size, Size, Size), GraphicsUnit.Pixel);

                    Tile tile = new Tile();
                    tile.Image = tileImage;
                    int hash = tile.GetHashCode();

                    bool found = false;
                    if (dic.ContainsKey(hash))
                    {
                        foreach (Tile t in dic[hash])
                        {
                            if (t.IsEqualImage(tile))
                            {
                                found = true;
                                tiles[x, y] = t;
                                break;
                            }
                        }
                    }

                    if (!found && AddSet != null && AddSet.Tiles.Count < 1000)
                    {
                        tile.Set = AddSet.Name;
                        tile.Size = new Size(Size, Size);
                        AddSet.Add(tile);

                        if (dic.ContainsKey(hash))
                        {
                            dic[hash].Add(tile);
                        }
                        else
                        {
                            List<Tile> list = new List<Tile>();
                            list.Add(tile);
                            dic.Add(tile.GetHashCode(), list);
                        }

                        tiles[x, y] = tile;
                    }
                }
            }
        }

        public void Save(Stream File, List<TileSet> TileSets)
        {
            BinaryWriter writer = new BinaryWriter(File);
            writer.Write("Burntime Map");
            writer.Write("0.2");
            
            // header
            writer.Write(Size.Width);
            writer.Write(Size.Height);
            writer.Write(TileSize);

            // get used tile set list
            Dictionary<String, bool> used = new Dictionary<string, bool>();
            for (int i = 0; i < TileSets.Count; i++)
                used.Add(TileSets[i].Name, false);
            for (int y = 0; y < Size.Height; y++)
            {
                for (int x = 0; x < Size.Width; x++)
                {
                    if (tiles[x, y] != null)
                    {
                        used[tiles[x, y].Set] = true;
                    }
                }
            }

            List<String> usedList = new List<string>();
            for (int i = 0; i < TileSets.Count; i++)
            {
                if (!used[TileSets[i].Name])
                    continue;
                usedList.Add(TileSets[i].Name);
            }

            // set indices
            Dictionary<String, int> indices = new Dictionary<string,int>();
            writer.Write(usedList.Count);
            for (int i = 0; i < usedList.Count; i++)
            {
                indices.Add(usedList[i], i);
                writer.Write(usedList[i]);
            }

            // tiles
            for (int y = 0; y < Size.Height; y++)
            {
                for (int x = 0; x < Size.Width; x++)
                {
                    if (tiles[x, y] != null)
                    {
                        writer.Write(tiles[x, y].ID);
                        writer.Write(tiles[x, y].SubSet);
                        writer.Write((byte)indices[tiles[x, y].Set]);
                    }
                    else
                    {
                        writer.Write((byte)0);
                        writer.Write((byte)0);
                        writer.Write((byte)0);
                    }
                }
            }

            // entrances

            writer.Write((byte)entrances.Count);
            for (int i = 0; i < entrances.Count; i++)
            {
                writer.Write(entrances[i].Rect.X);
                writer.Write(entrances[i].Rect.Y);
                writer.Write(entrances[i].Rect.Width);
                writer.Write(entrances[i].Rect.Height);
            }

            // ways
            writer.Write(ways.Count);
            for (int i = 0; i < ways.Count; i++)
            {
                writer.Write((byte)ways[i].Days);
                writer.Write((byte)ways[i].Entrance[0]);
                writer.Write((byte)ways[i].Entrance[1]);

                writer.Write((byte)ways[i].Points.Count);
                for (int j = 0; j < ways[i].Points.Count; j++)
                {
                    writer.Write(ways[i].Points[j].X);
                    writer.Write(ways[i].Points[j].Y);
                }
            }

            writer.Flush();

            Saved = true;
            if (AttachedView != null)
                AttachedView.UpdateTitle();
        }

        public bool Open(Stream File, List<TileSet> TileSets)
        {
            BinaryReader reader = new BinaryReader(File);
            if (reader.ReadString() != "Burntime Map")
                return false;
            String ver = reader.ReadString();
            if (ver != "0.1" && ver != "0.2")
                return false;

            // header
            Size.Width = reader.ReadInt32();
            Size.Height = reader.ReadInt32();
            TileSize = reader.ReadInt32();

            tiles = new Tile[Size.Width, Size.Height];

            // set indices
            List<String> indices = new List<string>();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
                indices.Add(reader.ReadString());

            Dictionary<String, int> indices2 = new Dictionary<string,int>();
            for (int i = 0; i < TileSets.Count; i++)
                indices2.Add(TileSets[i].Name, i);

            // tiles
            for (int y = 0; y < Size.Height; y++)
            {
                for (int x = 0; x < Size.Width; x++)
                {
                    byte id = reader.ReadByte();
                    byte subset = reader.ReadByte();
                    byte set = reader.ReadByte();

                    if (subset == 0)
                    {
                    }
                    else
                    {
#warning // crashes if tile set was removed
                        tiles[x, y] = TileSets[indices2[indices[set]]].Find(subset, id);
                    }
                }
            }

            // entrances

            count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                Entrance e = new Entrance();

                e.Rect.X = reader.ReadInt32(); 
                e.Rect.Y = reader.ReadInt32(); 
                e.Rect.Width = reader.ReadInt32(); 
                e.Rect.Height = reader.ReadInt32();

                entrances.Add(e);
            }

            if (ver == "0.2")
            {
                count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    Way way = new Way();
                    way.Days = reader.ReadByte();
                    way.Entrance[0] = reader.ReadByte();
                    way.Entrance[1] = reader.ReadByte();

                    int wpc = reader.ReadByte();
                    for (int j = 0; j < wpc; j++)
                    {
                        Point pt = new Point();
                        pt.X = reader.ReadInt32();
                        pt.Y = reader.ReadInt32();
                        way.Points.Add(pt);
                    }
                }
            }

            Saved = true;
            if (AttachedView != null)
            {
                AttachedView.UpdateTitle();
                AttachedView.UpdateMap();
            }

            return true;
        }

        public void Close()
        {
        }
    }
}
