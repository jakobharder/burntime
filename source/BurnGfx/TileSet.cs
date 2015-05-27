using System;
using System.Collections.Generic;

using Burntime.Platform;
using Burntime.Platform.IO;

namespace Burntime.Data.BurnGfx
{
    public class TileSet
    {
        protected const int TileCount = 63;
        protected Tile[] tiles;
        protected String setFile;
        protected int mapId;

        public Tile[] Tiles
        {
            get
            {
                if (!loaded)
                    Load();
                return tiles;
            }
        }

        public TileSet()
        {
            tiles = new Tile[0x100];
            loaded = true;
        }

        //int index = 0;
        //public int AddTile(ByteBuffer buffer, String id)
        //{

        //    BurnGfx.ByteBuffer tmp = new BurnGfx.ByteBuffer(Tile.WIDTH, Tile.HEIGHT, new Burntime.Platform.Graphics.PixelColor[Tile.WIDTH * Tile.HEIGHT]);
        //    tmp.DrawBuffer(buffer, 0, 0);

        //    Tile tile = new Tile(tmp, id);
        //    tiles[index] = tile;
        //    index++;

        //    return index - 1;
        //}

        public TileSet(String setFile, int mapId)
        {
            this.setFile = setFile;
            this.mapId = mapId;
        }

        bool loaded = false;
        public void Load()
        {
            tiles = new Tile[TileCount];
            File file = FileSystem.GetFile(setFile);
            for (int i = 0; i < TileCount; i++)
            {
                if (file.IsEOF)
                    break;
                tiles[i] = new Tile(file, mapId);
            }

            loaded = true;
        }
    }
}
