using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.Resource;
using Burntime.Data.BurnGfx;

namespace Burntime.Classic.ResourceProcessor
{
    class LocationMapProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID ID, IResourceManager ResourceManager)
        {
            IDataProcessor processor = ResourceManager.GetDataProcessor("burngfxmap");
            MapData data = processor.Process(ID, ResourceManager) as MapData;

            return data;
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "locationmap" }; }
        }
    }
}
