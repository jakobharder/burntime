using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Data.BurnGfx.ResourceProcessor
{
    public class TileProcessor : ISpriteProcessor
    {
        public Vector2 Size { get { return new Vector2(32, 32); } }

        File file;
        int pos;
        int map;

        public void Process(ResourceID ID)
        {
            file = FileSystem.GetFile(ID.File);
            pos = ID.Index;

            if (ID.Custom != null)
                map = int.Parse(ID.Custom);
            else
                map = 0;
        }

        public void Render(System.IO.Stream s, int stride)
        {
            file.Seek(pos * (16 + 32 * 32), SeekPosition.Begin);
            Tile tile = new Tile(file, map);
            tile.Render(s, stride);
        }
    }
}
