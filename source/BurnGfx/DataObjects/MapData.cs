using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Platform.Graphics;

namespace Burntime.Data.BurnGfx
{
    public struct MapTile
    {
        public short Section;
        public short Item;
        public Sprite Image;
    }

    [Serializable]
    public enum RoomType
    {
        Normal,
        WaterSource,
        Rope,
        Scene,
        Trader,
        Doctor,
        Pub,
        Restaurant,
        Church
    }

    public struct MapEntrance
    {
        public Rect Area;
        public RoomType RoomType;
        public int Background;
        public string TitleId;
    }

    public class PathMask
    {
        public int Resolution;
        public int Width;
        public int Height;
        protected bool[] Mask;

        public PathMask(int Width, int Height, int Resolution)
        {
            this.Width = Width;
            this.Height = Height;
            this.Resolution = Resolution;
            Mask = new bool[Width * Height];
        }

        // return true if walkable
        public bool this[Vector2 pos]
        {
            get { if (pos.x < 0 || pos.y < 0 || pos.x >= Width || pos.y >= Height) return false; return Mask[pos.x + pos.y * Width]; }
            set { Mask[pos.x + pos.y * Width] = value; }
        }

        // return true if walkable
        public bool this[int x, int y]
        {
            get { if (x < 0 || y < 0 || x >= Width || y >= Height) return false; return Mask[x + y * Width]; }
            set { Mask[x + y * Width] = value; }
        }

        public bool IsWalkableMapPosition(Vector2 mapPosition)
        {
            return this[mapPosition / Resolution];
        }
    }

    public class MapData : DataObject
    {
        public int Width;
        public int Height;
        public MapTile[] Tiles;
        public MapEntrance[] Entrances;
        public PathMask Mask;

        protected Vector2 tileSize;
        public Vector2 TileSize
        {
            get { return tileSize; }
            set { tileSize = value; }
        }

        public Vector2 Size
        {
            get { return new Vector2(Width, Height); }
        }

        public MapTile this[Vector2 pos]
        {
            get { return Tiles[pos.x + pos.y * Width]; }
            set { Tiles[pos.x + pos.y * Width] = value; }
        }

        public MapTile this[int x, int y]
        {
            get { return Tiles[x + y * Width]; }
            set { Tiles[x + y * Width] = value; }
        }
    }
}
