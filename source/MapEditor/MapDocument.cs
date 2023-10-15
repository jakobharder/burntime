using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.IO.Pipes;
using System.Linq;
using Burntime.Data.BurnGfx;
using System.Reflection.Metadata;

namespace MapEditor
{
    public class MapDocument
    {
        public String FilePath;
        public String Title;
        public Size Size;
        public int TileSize;

        public TileSet CustomTiles { get; set; }

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

        public void Import(string filePath, int tileSize, List<TileSet> sharedTiles, TileSet newTiles)
        {
            using var bmp = new Bitmap(filePath);
            int widthTiles = bmp.Width / tileSize;
            int heightTiles = bmp.Height / tileSize;

            var dic = new Dictionary<int, List<Tile>>();

            foreach (TileSet set in sharedTiles)
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
                        dic.Add(tile.GetHashCode(), new List<Tile>() { tile });
                    }
                }
            }

            Size = new Size(widthTiles, heightTiles);
            TileSize = tileSize;
            tiles = new Tile[widthTiles, heightTiles];

            for (int y = 0; y < heightTiles; y++)
            {
                for (int x = 0; x < widthTiles; x++)
                {
                    var tile = new Tile() { Image = bmp.ExtractTile(x, y, tileSize, tileSize) };
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

                    if (!found && newTiles != null && newTiles.Tiles.Count < 1000)
                    {
                        tile.Set = newTiles.Name;
                        tile.Size = new Size(tileSize, tileSize);
                        newTiles.Add(tile);

                        if (dic.ContainsKey(hash))
                        {
                            dic[hash].Add(tile);
                        }
                        else
                        {
                            dic.Add(tile.GetHashCode(), new List<Tile>() { tile });
                        }

                        tiles[x, y] = tile;
                    }
                }
            }
        }

        static string GetTilesPath(string filePath)
        {
            return Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_tiles.png");
        }

        static string GetSharedTilesPath(string filePath, int subset)
        {
            return Path.Combine(Path.GetDirectoryName(filePath), "shared", $"set_{subset}.png");
        }

        string[] GetUsedTileSets(List<TileSet> sharedTileSets)
        {
            Dictionary<string, bool> used = new();
            for (int i = 0; i < sharedTileSets.Count; i++)
                used.Add(sharedTileSets[i].Name, false);
            if (CustomTiles is not null)
                used.Add(CustomTiles.Name, false);
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

            return used.Keys.ToArray();
        }

        static Dictionary<string, int> GetTileSetMapping(string[] tileSets)
        {
            Dictionary<string, int> mapping = new();
            for (int i = 0; i < tileSets.Length; i++)
                mapping.Add(tileSets[i], i);
            return mapping;
        }

        public void Save(string filePath, List<TileSet> sharedTileSets)
        {
            using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write);

            BinaryWriter writer = new BinaryWriter(fileStream);
            writer.Write("Burntime Map");
            writer.Write("0.2");
            
            // header
            writer.Write(Size.Width);
            writer.Write(Size.Height);
            writer.Write(TileSize);

            var usedList = GetUsedTileSets(sharedTileSets);
            writer.Write(usedList.Length);
            for (int i = 0; i < usedList.Length; i++)
                writer.Write(usedList[i]);
            var tileSets = GetTileSetMapping(usedList);

            // tiles
            for (int y = 0; y < Size.Height; y++)
            {
                for (int x = 0; x < Size.Width; x++)
                {
                    if (tiles[x, y] != null)
                    {
                        writer.Write(tiles[x, y].ID);
                        writer.Write(tiles[x, y].SubSet);
                        writer.Write((byte)tileSets[tiles[x, y].Set]);
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

            CustomTiles?.Save(GetTilesPath(filePath), updateSheet: false);

            Saved = true;
            if (AttachedView != null)
                AttachedView.UpdateTitle();
        }

        public bool Open(string filePath, List<TileSet> sharedTiles)
        {
            using var File = new FileStream(filePath, FileMode.Open, FileAccess.Read);

            BinaryReader reader = new BinaryReader(File);
            if (reader.ReadString() != "Burntime Map")
                return false;
            String ver = reader.ReadString();
            if (ver != "0.1" && ver != "0.2")
                return false;

            string tilesPath = GetTilesPath(filePath);
            if (System.IO.File.Exists(tilesPath))
            {
                CustomTiles = new TileSet() { Name = "_" };
                CustomTiles.Load(tilesPath);
            }

            // header
            Size.Width = reader.ReadInt32();
            Size.Height = reader.ReadInt32();
            TileSize = reader.ReadInt32();

            tiles = new Tile[Size.Width, Size.Height];

            // set indices
            var usedTiles = new Dictionary<int, TileSet>();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string tileSetName = reader.ReadString();
                var tileSet = tileSetName == "_" ? CustomTiles : sharedTiles.Find((e) => e.Name == tileSetName);
                if (tileSet is not null)
                    usedTiles[i] = tileSet;
            }

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
                        if (usedTiles.ContainsKey(set))
                        {
                            tiles[x, y] = usedTiles[set].Find(subset, id);
                        }
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

        public void ConvertToMegaTile()
        {
            tiles = null;
            TileSize = 0;
        }

        public void UpdateUpscaled(string filePath, List<TileSet> sharedTileSets, Bitmap mapImage)
        {
            string[] usedList = GetUsedTileSets(sharedTileSets);
            HashSet<int> classicSubsets = new();

            Image inputImage = Image.FromFile(filePath).EnsureProperScale(Size.Width, Size.Height);

            for (int y = 0; y < Size.Height; y++)
            {
                for (int x = 0; x < Size.Width; x++)
                {
                    var tile = tiles[x, y];
                    if (tile != null)
                    {
                        tile.Upscaled = inputImage.ExtractTile(x, y, Tile.UPSCALE_WIDTH, Tile.UPSCALE_HEIGHT);
                        if (tile.Set == "classic")
                        {
                            classicSubsets.Add(tile.SubSet);
                        }
                    }
                }
            }

            inputImage.Dispose();

            if (CustomTiles is not null)
            {
                var tileSetPath = GetTilesPath(FilePath);
                tileSetPath = tileSetPath.Replace(".png", "_2x.png");
                CustomTiles.Save(tileSetPath, true, mapImage, updateSheet: false);
            }

            var classicSet = sharedTileSets.Find(e => e.Name == "classic");
            if (classicSet is not null)
            {
                foreach (int subset in classicSubsets)
                {
                    var tileSetPath = GetSharedTilesPath(FilePath, subset);
                    classicSet.GetSubset(subset).Save(tileSetPath, true, mapImage);
                }
            }
        }
    }
}
