using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Data.BurnGfx.ResourceProcessor
{
    public class TileMaskProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID ID, IResourceManager ResourceManager)
        {
            File file = FileSystem.GetFile(ID.File);

            file.Seek(ID.Index * (16 + 32 * 32), SeekPosition.Begin);
            Tile tile = new Tile(file, 0);

            TileMaskData data = new TileMaskData();
            data.Mask = new bool[16];
            data.DataName = ID;

            bool mask = false;
            bool nomask = false;

            int value = tile.Mask;
            for (int i = 0; i < 16; i++, value >>= 1)
            {
                data.Mask[15 - i] = (value & 1) == 0;
                mask |= data.Mask[15 - i];
                nomask |= !data.Mask[15 - i];
            }

            if (mask && nomask)
                data.Type = TileMaskType.Complex;
            else
            {
                data.Type = mask ? TileMaskType.Simple : TileMaskType.None;
            }

            return data;
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "burngfxtilemask" }; }
        }
    }
}
