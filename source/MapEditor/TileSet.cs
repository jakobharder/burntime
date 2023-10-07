using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Reflection.Metadata.Ecma335;
using System.Linq;

namespace MapEditor
{
    public class TileComparer : IComparer<Tile>
    {
        public int Compare(Tile left, Tile right)
        {
            if (left.SubSet == right.SubSet && left.ID == right.ID)
                return 0;
            if (left.SubSet < right.SubSet || left.SubSet == right.SubSet && left.ID < right.ID)
                return -1;

            return 1;
        }
    }

    public class TileSet
    {
        public List<Tile> Tiles = new List<Tile>();
        public String Name;

        public byte LastSubSet;
        public byte LastId;

        public void Sort()
        {
            Tiles.Sort(new TileComparer());
        }

        public Tile Find(byte subset, byte id)
        {
            //for (int i = 0; i < Tiles.Count; i++)
            //{
            //    if (Tiles[i].ID == id && Tiles[i].SubSet == subset)
            //        return Tiles[i];
            //}

            //return null;
            Tile t = new Tile();
            t.SubSet = subset;
            t.ID = id;

            int index = Tiles.BinarySearch(t, new TileComparer());
            if (index < 0)
                return null;

            return Tiles[index];
        }

        public void CalcLast()
        {
            LastSubSet = 0;
            LastId = 0;

            for (int i = 0; i < Tiles.Count; i++)
            {
                if (Tiles[i].SubSet > LastSubSet)
                    LastSubSet = Tiles[i].SubSet;
                if (Tiles[i].SubSet == LastSubSet && Tiles[i].ID > LastId)
                    LastId = Tiles[i].ID;
            }
        }

        public void Add(Tile Tile)
        {
            if (LastId == Tile.LAST_ID)
            {
                LastId = 0;
                LastSubSet++;
            }
            else
                LastId++;

            Tile.ID = LastId;
            Tile.SubSet = LastSubSet;
            Tiles.Add(Tile);
        }

        public void Save(string filePath, bool upscaled = false, Bitmap paletteReference = null)
        {
            int tileWidth = upscaled ? 64 : 32;
            int tileHeight = upscaled ? 76 : 32;

            int columns = Tiles.Count > 64 ? 16 : 8;
            int rows = (int)Math.Ceiling(Tiles.Count / (float)columns);

            Bitmap tileSheet = new Bitmap(columns * tileWidth, rows * tileHeight, PixelFormat.Format24bppRgb);
            //if (System.IO.File.Exists(filePath))
            //{

            //}

            int tileIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns && tileIndex < Tiles.Count; column++)
                {
                    Graphics g = Graphics.FromImage(tileSheet);
                    var tileImage = upscaled ? Tiles[tileIndex].Upscaled : Tiles[tileIndex].Image;
                    if (tileImage is not null)
                    {
                        g.DrawImage(tileImage,
                            new Rectangle(column * tileWidth, row * tileHeight, tileWidth, tileHeight),
                            0, 0,
                            tileWidth, tileHeight,
                            GraphicsUnit.Pixel);
                    }

                    tileIndex++;
                }
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
            tileSheet.Save8Bit(filePath, paletteReference);
            //tileSheet.Save(filePath);
        }

        public TileSet GetSubset(int subset)
        {
            return new TileSet()
            {
                Name = Name,
                Tiles = Tiles.Where(e => e.SubSet == subset).ToList()
            };
        }

        public void Load(string filePath)
        {
            const int TILE_SIZE = 32;

            var tileSheet = Bitmap.FromFile(filePath);

            int columns = tileSheet.Width / TILE_SIZE;
            int rows = tileSheet.Height / TILE_SIZE;

            int tileIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    var tile = new Bitmap(TILE_SIZE, TILE_SIZE, PixelFormat.Format24bppRgb);

                    Graphics g = Graphics.FromImage(tile);
                    g.DrawImage(tileSheet,
                        new Rectangle(0, 0, TILE_SIZE, TILE_SIZE),
                        column * TILE_SIZE, row * TILE_SIZE,
                        TILE_SIZE, TILE_SIZE,
                        GraphicsUnit.Pixel);

                    Tiles.Add(new Tile()
                    {
                        ID = (byte)(tileIndex % (Tile.LAST_ID + 1)),
                        Set = Name,
                        Size = new Size(TILE_SIZE, TILE_SIZE),
                        SubSet = (byte)(Tile.FIRST_SUBSET + (tileIndex / (Tile.LAST_ID + 1))),
                        Image = tile
                    }); ;
                    tileIndex++;
                }
            }
        }
    }
}
