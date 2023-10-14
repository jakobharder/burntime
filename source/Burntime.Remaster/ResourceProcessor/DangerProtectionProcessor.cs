using System;
using System.Collections.Generic;
using System.Text;

using Burntime.Platform.Resource;
using Burntime.Remaster.Logic.Interaction;

namespace Burntime.Remaster.ResourceProcessor
{
    class DangerProtectionProcessor : IDataProcessor
    {
        public DataObject Process(ResourceID id, IResourceManager resourceManager)
        {
            int value = 0;
            if (!int.TryParse(id.Custom, out value))
                Burntime.Platform.Log.Warning("DangerProtectionProcessor: custom parameter is not an integer.");

            return new DangerProtection(id.File.ToLower(), value * 0.01f);
        }

        string[] IDataProcessor.Names
        {
            get { return new string[] { "protection" }; }
        }
    }
}
