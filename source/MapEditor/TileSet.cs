
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

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
