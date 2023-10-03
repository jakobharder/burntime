using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.Resource;
using Burntime.Data.BurnGfx;

namespace Burntime.Classic.ResourceProcessor
{
    class ClassicMapProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID ID, IResourceManager ResourceManager)
        {
            IDataProcessor processor = ResourceManager.GetDataProcessor("burngfxmap");
            MapData data = processor.Process(ID, ResourceManager) as MapData;
            
            int map = 0;
            if (ID.Custom != null)
                map = int.Parse(ID.Custom);

            for (int i = 0; i < data.Tiles.Length; i++)
                data.Tiles[i].Image = ResourceManager.GetImage("burngfxtile@zei_" + data.Tiles[i].Section.ToString("D3") + ".raw?" + data.Tiles[i].Item.ToString() + "?" + map, ResourceLoadType.Delayed);

            return data;
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "classicmap" }; }
        }
    }
}
