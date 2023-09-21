using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

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
    }
}
